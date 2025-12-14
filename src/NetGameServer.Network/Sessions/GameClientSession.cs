using System.Buffers;
using System.Net.Sockets;
using System.Threading.Channels;
using NetGameServer.Common.Packets;
using NetGameServer.Network.Processing;
using static NetGameServer.Common.Packets.PacketPriority;
using Serilog;

namespace NetGameServer.Network.Sessions;

// 게임용 클라이언트 세션. 네트워크 I/O와 패킷 처리를 분리했습니다.
public class GameClientSession : IClientSession, IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly PacketBuffer _receiveBuffer;
    private readonly Channel<PacketBase> _sendQueue;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    
    private readonly byte[] _readBuffer = new byte[4096];
    private bool _disposed = false;
    private Task? _receiveTask;
    private Task? _sendTask;
    
    private PacketProcessor? _packetProcessor;
    private DateTime _lastActivityTime = DateTime.UtcNow;
    
    public string SessionId { get; }
    public bool IsConnected => _tcpClient.Connected && !_disposed;
    public DateTime LastActivityTime => _lastActivityTime;
    
    public event EventHandler<PacketBase>? PacketReceived;
    public event EventHandler? Disconnected;
    
    public GameClientSession(TcpClient tcpClient, PacketProcessor? packetProcessor = null)
    {
        _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
        ConfigureTcpClient(_tcpClient);
        _stream = _tcpClient.GetStream();
        _packetProcessor = packetProcessor;
        SessionId = Guid.NewGuid().ToString();
        _lastActivityTime = DateTime.UtcNow;
        
        _receiveBuffer = new PacketBuffer();
        
        // 송신 큐 설정
        var sendOptions = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _sendQueue = Channel.CreateBounded<PacketBase>(sendOptions);
        
        // 수신/송신 태스크 시작 (네트워크 전용 스레드)
        _receiveTask = Task.Run(ReceiveLoopAsync);
        _sendTask = Task.Run(SendLoopAsync);
    }
    
    /// <summary>
    /// TCP 소켓 옵션 최적화
    /// </summary>
    private void ConfigureTcpClient(TcpClient tcpClient)
    {
        try
        {
            // Nagle 알고리즘 비활성화 (지연 감소)
            tcpClient.NoDelay = true;
            
            // 버퍼 크기 증가
            tcpClient.ReceiveBufferSize = 65536; // 64KB
            tcpClient.SendBufferSize = 65536;    // 64KB
            
            // 타임아웃 설정
            tcpClient.ReceiveTimeout = 30000;    // 30초
            tcpClient.SendTimeout = 30000;       // 30초
            
            // Keep-Alive 설정 (플랫폼별로 다를 수 있음)
            var socket = tcpClient.Client;
            if (socket != null)
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                
                // Windows/Linux에서 Keep-Alive 옵션 설정
                // 참고: macOS에서는 추가 설정이 필요할 수 있음
                try
                {
                    // TCP Keep-Alive 간격 설정 (일부 플랫폼에서만 지원)
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
                }
                catch
                {
                    // 플랫폼에서 지원하지 않는 경우 무시
                }
            }
            
            Log.Debug("TCP 소켓 옵션 설정 완료: 세션 {SessionId}", SessionId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TCP 소켓 옵션 설정 실패: 세션 {SessionId}", SessionId);
        }
    }
    
    // 수신 루프. 네트워크 스레드에서만 실행됩니다.
    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (IsConnected)
            {
                var bytesRead = await _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length);
                if (bytesRead == 0)
                {
                    await DisconnectAsync();
                    break;
                }
                
                // 버퍼에 데이터 추가
                _receiveBuffer.Append(_readBuffer, bytesRead);
                
                // 활동 시간 업데이트
                _lastActivityTime = DateTime.UtcNow;
                
                // 완전한 패킷들 추출
                var completePackets = _receiveBuffer.ExtractCompletePackets();
                
                foreach (var packetData in completePackets)
                {
                    var packet = PacketFactory.DeserializePacket(packetData);
                    if (packet != null)
                    {
                        // 하트비트 패킷은 활동 시간만 업데이트하고 처리하지 않음
                        if (packet.PacketId == (ushort)PacketType.Heartbeat)
                        {
                            _lastActivityTime = DateTime.UtcNow;
                            continue;
                        }
                        
                        if (_packetProcessor != null)
                        {
                            var priority = PacketPriority.GetPriority((PacketType)packet.PacketId);
                            _packetProcessor.EnqueuePacket(this, packet, priority);
                        }
                        else
                        {
                            PacketReceived?.Invoke(this, packet);
                        }
                    }
                }
            }
        }
        catch (SocketException ex)
        {
            // 소켓 오류 (연결 끊김, 타임아웃 등)
            Log.Warning(ex, "세션 {SessionId} 소켓 오류: {SocketErrorCode} - {Message}", 
                SessionId, ex.SocketErrorCode, ex.Message);
            await DisconnectAsync();
        }
        catch (IOException ex)
        {
            // I/O 오류 (스트림 오류 등)
            Log.Warning(ex, "세션 {SessionId} I/O 오류: {Message}", SessionId, ex.Message);
            await DisconnectAsync();
        }
        catch (ObjectDisposedException)
        {
            // 이미 종료된 객체 접근
            Log.Debug("세션 {SessionId} 이미 종료됨", SessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "세션 {SessionId} 수신 오류: {Message}", SessionId, ex.Message);
            await DisconnectAsync();
        }
    }
    
    // 송신 루프. 네트워크 스레드에서만 실행됩니다.
    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var packet in _sendQueue.Reader.ReadAllAsync())
            {
                if (!IsConnected)
                    break;
                    
                await SendPacketInternalAsync(packet);
            }
        }
        catch (SocketException ex)
        {
            Log.Warning(ex, "세션 {SessionId} 송신 루프 소켓 오류: {SocketErrorCode} - {Message}", 
                SessionId, ex.SocketErrorCode, ex.Message);
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "세션 {SessionId} 송신 루프 I/O 오류: {Message}", SessionId, ex.Message);
        }
        catch (ObjectDisposedException)
        {
            Log.Debug("세션 {SessionId} 송신 루프 종료됨", SessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "세션 {SessionId} 송신 루프 오류: {Message}", SessionId, ex.Message);
        }
    }
    
    // 패킷 전송. 게임 로직 스레드에서도 호출 가능합니다.
    public async Task SendPacketAsync(PacketBase packet)
    {
        if (!IsConnected)
            return;
            
        try
        {
            await _sendQueue.Writer.WriteAsync(packet);
        }
        catch (ChannelClosedException)
        {
            // 채널이 닫힌 경우 (정상 종료)
            Log.Debug("세션 {SessionId} 전송 큐 닫힘", SessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "세션 {SessionId} 전송 큐 오류: {Message}", SessionId, ex.Message);
            await DisconnectAsync();
        }
    }
    
    // 실제 패킷 전송. 네트워크 스레드에서만 실행됩니다.
    private async Task SendPacketInternalAsync(PacketBase packet)
    {
        await _sendSemaphore.WaitAsync();
        try
        {
            var data = packet.Serialize();
            var lengthBytes = BitConverter.GetBytes(data.Length);
            
            // ArrayPool을 사용해 전송 버퍼 할당
            var totalLength = lengthBytes.Length + data.Length;
            var sendBuffer = _arrayPool.Rent(totalLength);
            try
            {
                Buffer.BlockCopy(lengthBytes, 0, sendBuffer, 0, lengthBytes.Length);
                Buffer.BlockCopy(data, 0, sendBuffer, lengthBytes.Length, data.Length);
                
                await _stream.WriteAsync(new ReadOnlyMemory<byte>(sendBuffer, 0, totalLength));
                await _stream.FlushAsync();
                
                Log.Debug("패킷 전송 완료: 세션 {SessionId}, 패킷 ID: {PacketId}, 크기: {Size} bytes", 
                    SessionId, packet.PacketId, totalLength);
            }
            finally
            {
                _arrayPool.Return(sendBuffer);
            }
        }
        catch (SocketException ex)
        {
            Log.Warning(ex, "세션 {SessionId} 전송 소켓 오류: {SocketErrorCode} - {Message}", 
                SessionId, ex.SocketErrorCode, ex.Message);
            await DisconnectAsync();
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "세션 {SessionId} 전송 I/O 오류: {Message}", SessionId, ex.Message);
            await DisconnectAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "세션 {SessionId} 전송 오류: {Message}", SessionId, ex.Message);
            await DisconnectAsync();
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }
    
    public async Task DisconnectAsync()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        
        try
        {
            _sendQueue.Writer.Complete();
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch { }
        
        Disconnected?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask;
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            DisconnectAsync().Wait();
            _receiveTask?.Wait();
            _sendTask?.Wait();
            _tcpClient?.Dispose();
            _receiveBuffer.Dispose();
            _sendSemaphore.Dispose();
            _disposed = true;
        }
    }
}

