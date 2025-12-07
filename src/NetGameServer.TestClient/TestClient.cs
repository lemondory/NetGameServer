using System.Net;
using System.Net.Sockets;
using NetGameServer.Common.Packets;
using Serilog;

namespace NetGameServer.TestClient;

/// <summary>
/// 테스트 클라이언트
/// </summary>
public class TestClient : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly byte[] _receiveBuffer = new byte[4096];
    private readonly List<byte> _packetBuffer = new();
    private bool _isConnected = false;
    private Task? _receiveTask;
    
    public string ClientId { get; }
    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
    
    public event EventHandler<PacketBase>? PacketReceived;
    
    public TestClient()
    {
        ClientId = Guid.NewGuid().ToString().Substring(0, 8);
    }
    
    /// <summary>
    /// 서버에 연결
    /// </summary>
    public async Task<bool> ConnectAsync(IPAddress address, int port)
    {
        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(address, port);
            _stream = _tcpClient.GetStream();
            _isConnected = true;
            
            // 수신 루프 시작
            _receiveTask = Task.Run(ReceiveLoopAsync);
            
            Log.Information("[{ClientId}] 서버에 연결됨: {Address}:{Port}", ClientId, address, port);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{ClientId}] 연결 실패", ClientId);
            return false;
        }
    }
    
    /// <summary>
    /// 패킷 전송
    /// </summary>
    public async Task<bool> SendPacketAsync(PacketBase packet)
    {
        if (!IsConnected || _stream == null)
        {
            Log.Warning("[{ClientId}] 연결되지 않음", ClientId);
            return false;
        }
        
        try
        {
            var data = packet.Serialize();
            var lengthBytes = BitConverter.GetBytes(data.Length);
            
            // 패킷 크기 + 패킷 데이터 전송
            await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
            
            Log.Debug("[{ClientId}] 패킷 전송: {PacketId} ({Size} bytes)", ClientId, packet.PacketId, data.Length);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{ClientId}] 패킷 전송 실패", ClientId);
            return false;
        }
    }
    
    /// <summary>
    /// 패킷 수신 대기
    /// </summary>
    public async Task<PacketBase?> ReceivePacketAsync(TimeSpan timeout, ushort? expectedPacketId = null)
    {
        var startTime = DateTime.UtcNow;
        PacketBase? receivedPacket = null;
        
        EventHandler<PacketBase>? handler = null;
        var tcs = new TaskCompletionSource<PacketBase?>();
        
        handler = (sender, packet) =>
        {
            // 특정 패킷 타입을 기다리는 경우 필터링
            if (expectedPacketId.HasValue && packet.PacketId != expectedPacketId.Value)
            {
                Log.Debug("[{ClientId}] 기대한 패킷이 아님: 기대 {ExpectedId}, 수신 {ReceivedId}", 
                    ClientId, expectedPacketId.Value, packet.PacketId);
                return; // 이 패킷은 무시하고 계속 대기
            }
            
            receivedPacket = packet;
            tcs.TrySetResult(packet);
        };
        
        PacketReceived += handler;
        
        try
        {
            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                if (expectedPacketId.HasValue)
                {
                    Log.Warning("[{ClientId}] 패킷 수신 타임아웃: 기대한 패킷 ID {ExpectedId} ({Timeout}초)", 
                        ClientId, expectedPacketId.Value, timeout.TotalSeconds);
                }
                else
                {
                    Log.Warning("[{ClientId}] 패킷 수신 타임아웃 ({Timeout}초)", ClientId, timeout.TotalSeconds);
                }
                return null;
            }
            
            var result = await tcs.Task;
            if (result != null)
            {
                Log.Debug("[{ClientId}] 패킷 수신 완료: {PacketId}", ClientId, result.PacketId);
            }
            return result;
        }
        finally
        {
            PacketReceived -= handler;
        }
    }
    
    /// <summary>
    /// 수신 루프
    /// </summary>
    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (IsConnected && _stream != null)
            {
                var bytesRead = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                if (bytesRead == 0)
                {
                    Log.Information("[{ClientId}] 연결 종료됨", ClientId);
                    break;
                }
                
                ProcessReceivedData(_receiveBuffer, bytesRead);
            }
        }
        catch (OperationCanceledException)
        {
            // 연결 종료 시 정상적인 취소 - 로그 레벨 낮춤
            Log.Debug("[{ClientId}] 수신 루프 종료 (연결 종료)", ClientId);
        }
        catch (Exception ex)
        {
            // 실제 오류만 에러 로그로 기록
            if (ex is not IOException || !ex.Message.Contains("Operation canceled"))
            {
                Log.Error(ex, "[{ClientId}] 수신 오류", ClientId);
            }
            else
            {
                Log.Debug("[{ClientId}] 연결 종료로 인한 수신 중단 (정상)", ClientId);
            }
        }
        finally
        {
            _isConnected = false;
        }
    }
    
    /// <summary>
    /// 수신된 데이터 처리
    /// </summary>
    private void ProcessReceivedData(byte[] data, int length)
    {
        Log.Debug("[{ClientId}] 원시 데이터 수신: {Length} bytes", ClientId, length);
        _packetBuffer.AddRange(data.Take(length));
        Log.Debug("[{ClientId}] 버퍼 크기: {BufferSize} bytes", ClientId, _packetBuffer.Count);
        
        // 패킷 크기(4바이트) + 패킷 데이터 처리
        while (_packetBuffer.Count >= sizeof(int))
        {
            // 패킷 크기 읽기
            var packetSize = BitConverter.ToInt32(_packetBuffer.ToArray(), 0);
            Log.Debug("[{ClientId}] 패킷 크기 읽기: {PacketSize} bytes", ClientId, packetSize);
            
            // 패킷 크기 유효성 검사
            if (packetSize < 0 || packetSize > 1024 * 1024)
            {
                Log.Warning("[{ClientId}] 잘못된 패킷 크기: {PacketSize}, 버퍼 초기화", ClientId, packetSize);
                _packetBuffer.Clear();
                break;
            }
            
            var totalSize = sizeof(int) + packetSize;
            if (_packetBuffer.Count < totalSize)
            {
                // 패킷이 완전히 수신되지 않음
                Log.Debug("[{ClientId}] 패킷이 완전히 수신되지 않음: 필요 {TotalSize} bytes, 현재 {CurrentSize} bytes", 
                    ClientId, totalSize, _packetBuffer.Count);
                break;
            }
            
            // 패킷 데이터 추출
            var packetData = _packetBuffer.Skip(sizeof(int)).Take(packetSize).ToArray();
            _packetBuffer.RemoveRange(0, totalSize);
            Log.Debug("[{ClientId}] 패킷 데이터 추출 완료: {Size} bytes", ClientId, packetData.Length);
            
            // 패킷 역직렬화
            try
            {
                var packet = PacketFactory.DeserializePacket(packetData);
                if (packet != null)
                {
                    Log.Information("[{ClientId}] 패킷 수신 성공: {PacketId} ({Size} bytes)", ClientId, packet.PacketId, packetData.Length);
                    PacketReceived?.Invoke(this, packet);
                }
                else
                {
                    // 패킷 타입을 읽어서 로그 남기기
                    if (packetData.Length >= 2)
                    {
                        var packetId = BitConverter.ToUInt16(packetData, 0);
                        Log.Warning("[{ClientId}] 알 수 없는 패킷 타입: {PacketId} ({Size} bytes), 데이터: {Data}", 
                            ClientId, packetId, packetData.Length, Convert.ToHexString(packetData.Take(Math.Min(32, packetData.Length)).ToArray()));
                    }
                    else
                    {
                        Log.Warning("[{ClientId}] 패킷 데이터가 너무 짧음: {Size} bytes", ClientId, packetData.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{ClientId}] 패킷 역직렬화 실패: {Size} bytes, 데이터: {Data}", 
                    ClientId, packetData.Length, Convert.ToHexString(packetData.Take(Math.Min(32, packetData.Length)).ToArray()));
            }
        }
    }
    
    /// <summary>
    /// 연결 종료
    /// </summary>
    public async Task DisconnectAsync()
    {
        _isConnected = false;
        
        try
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch { }
        
        if (_receiveTask != null)
        {
            await Task.WhenAny(_receiveTask, Task.Delay(1000));
        }
        
        Log.Information("[{ClientId}] 연결 종료됨", ClientId);
    }
    
    public void Dispose()
    {
        DisconnectAsync().Wait();
        _tcpClient?.Dispose();
    }
}

