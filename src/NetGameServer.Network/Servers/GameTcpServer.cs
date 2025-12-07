using System.Net;
using System.Net.Sockets;
using NetGameServer.Network.Management;
using NetGameServer.Network.Processing;
using NetGameServer.Network.Sessions;

namespace NetGameServer.Network.Servers;

/// <summary>
/// 게임용 TCP 서버 - ConnectionManager와 PacketProcessor 통합
/// </summary>
public class GameTcpServer : IDisposable
{
    private TcpListener? _listener;
    private readonly ConnectionManager _connectionManager;
    private readonly PacketProcessor _packetProcessor;
    private bool _isRunning = false;
    private CancellationTokenSource? _cancellationTokenSource;
    
    public event EventHandler<IClientSession>? ClientConnected;
    public event EventHandler<IClientSession>? ClientDisconnected;
    
    /// <summary>
    /// 패킷 처리 핸들러 (게임 로직에서 설정)
    /// </summary>
    public Action<PacketContext>? PacketHandler
    {
        get => _packetProcessor.PacketHandler;
        set => _packetProcessor.PacketHandler = value ?? ((context) => { });
    }
    
    public GameTcpServer(int maxConnections = 10000, int packetWorkerCount = 4)
    {
        _connectionManager = new ConnectionManager(maxConnections);
        _packetProcessor = new PacketProcessor(packetWorkerCount);
        _packetProcessor.PacketHandler = OnPacketReceived;
        
        _connectionManager.ClientConnected += (s, session) => ClientConnected?.Invoke(this, session);
        _connectionManager.ClientDisconnected += (s, session) => ClientDisconnected?.Invoke(this, session);
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
        
        Console.WriteLine($"게임 TCP 서버 시작: {address}:{port}");
        Console.WriteLine($"최대 연결: {_connectionManager.MaxConnections}, 패킷 워커: {_packetProcessor.WorkerCount}");
        
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
        
        _connectionManager.Dispose();
        _packetProcessor.Dispose();
        
        Console.WriteLine("게임 TCP 서버 중지");
        await Task.CompletedTask;
    }
    
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                
                // 각 클라이언트를 별도 태스크로 처리
                _ = Task.Run(async () =>
                {
                    await HandleClientAsync(tcpClient, cancellationToken);
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
            }
        }
    }
    
    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        GameClientSession? session = null;
        try
        {
            // 패킷 프로세서를 사용하는 게임 세션 생성
            session = new GameClientSession(tcpClient, _packetProcessor);
            
            // 연결 관리자에 추가
            if (!_connectionManager.TryAddSession(session))
            {
                // 연결 수 초과
                await session.DisconnectAsync();
                return;
            }
            
            Console.WriteLine($"클라이언트 연결: {session.SessionId} (현재 연결: {_connectionManager.ActiveSessionCount})");
            
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
    /// 패킷 수신 처리 (패킷 프로세서에서 호출)
    /// </summary>
    private void OnPacketReceived(PacketContext context)
    {
        // 기본 구현 - 게임 로직에서 PacketHandler를 설정하여 오버라이드
        Console.WriteLine($"패킷 수신: {context.Session.SessionId}, 타입: {context.Packet.PacketId}");
    }
    
    /// <summary>
    /// 모든 세션에 패킷 브로드캐스트
    /// </summary>
    public async Task BroadcastAsync(NetGameServer.Common.Packets.PacketBase packet)
    {
        var sessions = _connectionManager.GetAllSessions().ToList();
        var tasks = sessions.Select(s => s.SendPacketAsync(packet));
        await Task.WhenAll(tasks);
    }
    
    /// <summary>
    /// 특정 세션에 패킷 전송
    /// </summary>
    public async Task SendToSessionAsync(string sessionId, NetGameServer.Common.Packets.PacketBase packet)
    {
        var session = _connectionManager.GetSession(sessionId);
        if (session != null && session.IsConnected)
        {
            await session.SendPacketAsync(packet);
        }
    }
    
    public int ActiveSessionCount => _connectionManager.ActiveSessionCount;
    public int AvailableConnections => _connectionManager.AvailableConnections;
    public int QueuedPacketCount => _packetProcessor.QueuedPacketCount;
    
    public void Dispose()
    {
        StopAsync().Wait();
        _listener?.Stop();
        _cancellationTokenSource?.Dispose();
    }
}

