using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using NetGameServer.Network.Sessions;

namespace NetGameServer.Network.Servers;

/// <summary>
/// 성능 최적화된 TCP 서버 (참고용)
/// </summary>
public class TcpServerOptimized : IDisposable
{
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, IClientSession> _sessions = new();
    private readonly SemaphoreSlim _connectionLimiter;
    private bool _isRunning = false;
    private CancellationTokenSource? _cancellationTokenSource;
    
    public event EventHandler<IClientSession>? ClientConnected;
    public event EventHandler<IClientSession>? ClientDisconnected;
    
    /// <summary>
    /// 최대 동시 연결 수
    /// </summary>
    public int MaxConnections { get; }
    
    public TcpServerOptimized(int maxConnections = 10000)
    {
        MaxConnections = maxConnections;
        _connectionLimiter = new SemaphoreSlim(maxConnections, maxConnections);
    }
    
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
        _cancellationTokenSource = new CancellationTokenSource();
        
        Console.WriteLine($"TCP 서버 시작: {address}:{port} (최대 연결: {MaxConnections})");
        
        // 비동기로 클라이언트 수락 시작
        _ = Task.Run(async () => await AcceptClientsAsync(_cancellationTokenSource.Token));
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
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
        
        // 모든 세션 종료
        var sessions = _sessions.Values.ToList();
        var tasks = sessions.Select(s => s.DisconnectAsync());
        await Task.WhenAll(tasks);
        
        _sessions.Clear();
        
        Console.WriteLine("TCP 서버 중지");
        await Task.CompletedTask;
    }
    
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 연결 수 제한
                await _connectionLimiter.WaitAsync(cancellationToken);
                
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                
                // 각 클라이언트를 별도 태스크로 처리 (확장성 향상)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleClientAsync(tcpClient, cancellationToken);
                    }
                    finally
                    {
                        _connectionLimiter.Release();
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 수락 오류: {ex.Message}");
                _connectionLimiter.Release();
            }
        }
    }
    
    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        IClientSession? session = null;
        try
        {
            // 최적화된 세션 사용 (또는 기본 세션)
            session = new ClientSession(tcpClient);
            
            session.Disconnected += (sender, e) =>
            {
                if (session != null)
                {
                    _sessions.TryRemove(session.SessionId, out _);
                    ClientDisconnected?.Invoke(this, session);
                }
            };
            
            _sessions.TryAdd(session.SessionId, session);
            ClientConnected?.Invoke(this, session);
            
            Console.WriteLine($"클라이언트 연결: {session.SessionId} (현재 연결: {_sessions.Count})");
            
            // 세션이 종료될 때까지 대기
            while (session.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"클라이언트 처리 오류: {ex.Message}");
        }
        finally
        {
            if (session != null)
            {
                await session.DisconnectAsync();
            }
        }
    }
    
    /// <summary>
    /// 모든 세션에 패킷 브로드캐스트
    /// </summary>
    public async Task BroadcastAsync(NetGameServer.Common.Packets.PacketBase packet)
    {
        var sessions = _sessions.Values
            .Where(s => s.IsConnected)
            .ToList();
            
        var tasks = sessions.Select(s => s.SendPacketAsync(packet));
        await Task.WhenAll(tasks);
    }
    
    /// <summary>
    /// 특정 세션에 패킷 전송
    /// </summary>
    public async Task SendToSessionAsync(string sessionId, NetGameServer.Common.Packets.PacketBase packet)
    {
        if (_sessions.TryGetValue(sessionId, out var session) && session.IsConnected)
        {
            await session.SendPacketAsync(packet);
        }
    }
    
    /// <summary>
    /// 활성 세션 수
    /// </summary>
    public int ActiveSessionCount => _sessions.Count(s => s.Value.IsConnected);
    
    /// <summary>
    /// 사용 가능한 연결 슬롯 수
    /// </summary>
    public int AvailableConnections => _connectionLimiter.CurrentCount;
    
    public void Dispose()
    {
        StopAsync().Wait();
        _listener?.Stop();
        _connectionLimiter.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

