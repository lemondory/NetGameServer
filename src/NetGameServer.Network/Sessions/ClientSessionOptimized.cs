using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;
using NetGameServer.Common.Packets;

namespace NetGameServer.Network.Sessions;

/// <summary>
/// 성능 최적화된 TCP 클라이언트 세션 (참고용)
/// </summary>
public class ClientSessionOptimized : IClientSession, IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private readonly Channel<PacketBase> _sendQueue;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    
    // 메모리 효율적인 버퍼 관리
    private byte[] _receiveBuffer;
    private int _receiveBufferOffset = 0;
    private const int InitialBufferSize = 4096;
    private const int MaxBufferSize = 1024 * 1024; // 1MB
    
    private bool _disposed = false;
    private Task? _receiveTask;
    private Task? _sendTask;
    
    public string SessionId { get; }
    public bool IsConnected => _tcpClient.Connected && !_disposed;
    
    public event EventHandler<PacketBase>? PacketReceived;
    public event EventHandler? Disconnected;
    
    public ClientSessionOptimized(TcpClient tcpClient)
    {
        _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
        _stream = _tcpClient.GetStream();
        SessionId = Guid.NewGuid().ToString();
        
        // Channel을 사용한 비동기 큐
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _sendQueue = Channel.CreateBounded<PacketBase>(options);
        
        // 버퍼는 ArrayPool에서 할당
        _receiveBuffer = _arrayPool.Rent(InitialBufferSize);
        
        // 수신/송신 태스크 시작
        _receiveTask = Task.Run(ReceiveLoopAsync);
        _sendTask = Task.Run(SendLoopAsync);
    }
    
    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (IsConnected)
            {
                // 버퍼 공간 확인 및 확장
                EnsureReceiveBufferCapacity();
                
                var bytesRead = await _stream.ReadAsync(
                    new Memory<byte>(_receiveBuffer, _receiveBufferOffset, 
                        _receiveBuffer.Length - _receiveBufferOffset));
                
                if (bytesRead == 0)
                {
                    await DisconnectAsync();
                    break;
                }
                
                _receiveBufferOffset += bytesRead;
                ProcessReceivedData();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"세션 {SessionId} 수신 오류: {ex.Message}");
            await DisconnectAsync();
        }
    }
    
    private void ProcessReceivedData()
    {
        // 패킷 크기(4바이트) + 패킷 데이터 처리
        while (_receiveBufferOffset >= sizeof(int))
        {
            // Span<T>를 사용한 Zero-copy 읽기
            var span = new ReadOnlySpan<byte>(_receiveBuffer, 0, _receiveBufferOffset);
            var packetSize = BitConverter.ToInt32(span);
            
            // 패킷 크기 유효성 검사
            if (packetSize < 0 || packetSize > MaxBufferSize)
            {
                Console.WriteLine($"잘못된 패킷 크기: {packetSize}");
                _receiveBufferOffset = 0;
                break;
            }
            
            var totalSize = sizeof(int) + packetSize;
            if (_receiveBufferOffset < totalSize)
            {
                // 패킷이 완전히 수신되지 않음
                break;
            }
            
            // 패킷 데이터 추출 (Span 사용)
            var packetData = span.Slice(sizeof(int), packetSize).ToArray();
            
            // 버퍼에서 처리된 데이터 제거 (메모리 이동 최소화)
            if (_receiveBufferOffset > totalSize)
            {
                Buffer.BlockCopy(_receiveBuffer, totalSize, _receiveBuffer, 0, 
                    _receiveBufferOffset - totalSize);
            }
            _receiveBufferOffset -= totalSize;
            
            // 패킷 역직렬화 및 이벤트 발생
            var packet = PacketFactory.DeserializePacket(packetData);
            if (packet != null)
            {
                // 비동기로 처리하여 블로킹 방지
                _ = Task.Run(() => PacketReceived?.Invoke(this, packet));
            }
        }
    }
    
    private void EnsureReceiveBufferCapacity()
    {
        if (_receiveBufferOffset >= _receiveBuffer.Length * 0.8)
        {
            var newSize = Math.Min(_receiveBuffer.Length * 2, MaxBufferSize);
            var newBuffer = _arrayPool.Rent(newSize);
            Buffer.BlockCopy(_receiveBuffer, 0, newBuffer, 0, _receiveBufferOffset);
            _arrayPool.Return(_receiveBuffer);
            _receiveBuffer = newBuffer;
        }
    }
    
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
        catch (Exception ex)
        {
            Console.WriteLine($"세션 {SessionId} 송신 루프 오류: {ex.Message}");
        }
    }
    
    public async Task SendPacketAsync(PacketBase packet)
    {
        if (!IsConnected)
            return;
            
        try
        {
            await _sendQueue.Writer.WriteAsync(packet);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"세션 {SessionId} 전송 큐 오류: {ex.Message}");
            await DisconnectAsync();
        }
    }
    
    private async Task SendPacketInternalAsync(PacketBase packet)
    {
        await _sendSemaphore.WaitAsync();
        try
        {
            var data = packet.Serialize();
            var lengthBytes = BitConverter.GetBytes(data.Length);
            
            // 한 번에 전송 (시스템 콜 최소화)
            var totalLength = lengthBytes.Length + data.Length;
            var sendBuffer = _arrayPool.Rent(totalLength);
            try
            {
                Buffer.BlockCopy(lengthBytes, 0, sendBuffer, 0, lengthBytes.Length);
                Buffer.BlockCopy(data, 0, sendBuffer, lengthBytes.Length, data.Length);
                
                await _stream.WriteAsync(new ReadOnlyMemory<byte>(sendBuffer, 0, totalLength));
                await _stream.FlushAsync();
            }
            finally
            {
                _arrayPool.Return(sendBuffer);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"세션 {SessionId} 전송 오류: {ex.Message}");
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
            _arrayPool.Return(_receiveBuffer);
            _sendSemaphore.Dispose();
            _disposed = true;
        }
    }
}

