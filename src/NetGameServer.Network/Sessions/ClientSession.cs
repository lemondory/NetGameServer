using System.Net.Sockets;
using NetGameServer.Common.Packets;

namespace NetGameServer.Network.Sessions;

/// <summary>
/// TCP 클라이언트 세션 구현
/// </summary>
public class ClientSession : IClientSession, IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly byte[] _buffer = new byte[4096];
    private readonly List<byte> _receiveBuffer = new();
    private bool _disposed = false;
    
    public string SessionId { get; }
    public bool IsConnected => _tcpClient.Connected && !_disposed;
    
    public event EventHandler<PacketBase>? PacketReceived;
    public event EventHandler? Disconnected;
    
    public ClientSession(TcpClient tcpClient)
    {
        _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
        _stream = _tcpClient.GetStream();
        SessionId = Guid.NewGuid().ToString();
        
        StartReceive();
    }
    
    private async void StartReceive()
    {
        try
        {
            while (IsConnected)
            {
                var bytesRead = await _stream.ReadAsync(_buffer, 0, _buffer.Length);
                if (bytesRead == 0)
                {
                    await DisconnectAsync();
                    break;
                }
                
                ProcessReceivedData(_buffer, bytesRead);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"세션 {SessionId} 수신 오류: {ex.Message}");
            await DisconnectAsync();
        }
    }
    
    private void ProcessReceivedData(byte[] data, int length)
    {
        _receiveBuffer.AddRange(data.Take(length));
        
        // 패킷 크기(4바이트) + 패킷 데이터 처리
        while (_receiveBuffer.Count >= sizeof(int))
        {
            // 패킷 크기 읽기
            var packetSize = BitConverter.ToInt32(_receiveBuffer.ToArray(), 0);
            
            // 패킷 크기가 유효한지 확인
            if (packetSize < 0 || packetSize > 1024 * 1024) // 최대 1MB
            {
                Console.WriteLine($"잘못된 패킷 크기: {packetSize}");
                _receiveBuffer.Clear();
                break;
            }
            
            // 패킷 크기 헤더(4바이트) + 패킷 데이터가 모두 수신되었는지 확인
            var totalSize = sizeof(int) + packetSize;
            if (_receiveBuffer.Count < totalSize)
            {
                // 패킷이 완전히 수신되지 않음
                break;
            }
            
            // 패킷 데이터 추출
            var packetData = _receiveBuffer.Skip(sizeof(int)).Take(packetSize).ToArray();
            _receiveBuffer.RemoveRange(0, totalSize);
            
            // 패킷 역직렬화
            var packet = PacketFactory.DeserializePacket(packetData);
            if (packet != null)
            {
                PacketReceived?.Invoke(this, packet);
            }
        }
    }
    
    public async Task SendPacketAsync(PacketBase packet)
    {
        if (!IsConnected)
            return;
            
        try
        {
            var data = packet.Serialize();
            var lengthBytes = BitConverter.GetBytes(data.Length);
            
            // 패킷 크기 + 패킷 데이터 전송
            await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"세션 {SessionId} 전송 오류: {ex.Message}");
            await DisconnectAsync();
        }
    }
    
    public async Task DisconnectAsync()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        
        try
        {
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
            _tcpClient?.Dispose();
            _disposed = true;
        }
    }
}

