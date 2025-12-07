using System.Net;
using System.Net.Sockets;

using NetGameServer.Network.Sessions;

namespace NetGameServer.Network.Servers;

/// <summary>
/// TCP 서버
/// </summary>
public class TcpServer : IDisposable
{
    private TcpListener? _listener;
    private readonly List<IClientSession> _sessions = new();
    private bool _isRunning = false;
    
    public event EventHandler<IClientSession>? ClientConnected;
    public event EventHandler<IClientSession>? ClientDisconnected;
    
    /// <summary>
    /// 서버 시작
    /// </summary>
    public async Task StartAsync(IPAddress address, int port)
    {
        if (_isRunning)
            return;
            
        _listener = new TcpListener(address, port);
        _listener.Start();
        _isRunning = true;
        
        Console.WriteLine($"TCP 서버 시작: {address}:{port}");
        
        _ = Task.Run(async () => await AcceptClientsAsync());
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 서버 중지
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
            return;
            
        _isRunning = false;
        _listener?.Stop();
        
        // 모든 세션 종료
        var sessions = _sessions.ToList();
        foreach (var session in sessions)
        {
            await session.DisconnectAsync();
        }
        
        _sessions.Clear();
        
        Console.WriteLine("TCP 서버 중지");
        await Task.CompletedTask;
    }
    
    private async Task AcceptClientsAsync()
    {
        while (_isRunning)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                var session = new ClientSession(tcpClient);
                
                session.Disconnected += (sender, e) =>
                {
                    _sessions.Remove(session);
                    ClientDisconnected?.Invoke(this, session);
                };
                
                _sessions.Add(session);
                ClientConnected?.Invoke(this, session);
                
                Console.WriteLine($"클라이언트 연결: {session.SessionId}");
            }
            catch (ObjectDisposedException)
            {
                // 서버가 중지됨
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 수락 오류: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 모든 세션에 패킷 브로드캐스트
    /// </summary>
    public async Task BroadcastAsync(NetGameServer.Common.Packets.PacketBase packet)
    {
        var sessions = _sessions.Where(s => s.IsConnected).ToList();
        var tasks = sessions.Select(s => s.SendPacketAsync(packet));
        await Task.WhenAll(tasks);
    }
    
    /// <summary>
    /// 활성 세션 수
    /// </summary>
    public int ActiveSessionCount => _sessions.Count(s => s.IsConnected);
    
    public void Dispose()
    {
        StopAsync().Wait();
        _listener?.Stop();
    }
}

