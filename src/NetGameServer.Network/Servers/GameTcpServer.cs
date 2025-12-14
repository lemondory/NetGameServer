using System.Net;
using System.Net.Sockets;
using NetGameServer.Network.Management;
using NetGameServer.Network.Processing;
using NetGameServer.Network.Sessions;
using Serilog;

namespace NetGameServer.Network.Servers;

/// <summary>
/// 게임용 TCP 서버 - ConnectionManager와 PacketProcessor 통합
/// </summary>
public class GameTcpServer : IDisposable
{
    private TcpListener? _listener;
    private readonly ConnectionManager _connectionManager;
    private readonly PacketProcessor _packetProcessor;
    private readonly HeartbeatManager _heartbeatManager;
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
    
    public GameTcpServer(
        int maxConnections = 10000, 
        int packetWorkerCount = 4,
        TimeSpan? heartbeatInterval = null,
        TimeSpan? heartbeatTimeout = null)
    {
        _connectionManager = new ConnectionManager(maxConnections);
        _packetProcessor = new PacketProcessor(packetWorkerCount);
        _packetProcessor.PacketHandler = OnPacketReceived;
        
        // 하트비트 관리자 초기화
        _heartbeatManager = new HeartbeatManager(
            heartbeatInterval: heartbeatInterval,
            timeout: heartbeatTimeout,
            onTimeout: OnSessionTimeout
        );
        
        _connectionManager.ClientConnected += (s, session) =>
        {
            _heartbeatManager.RegisterSession(session.SessionId);
            ClientConnected?.Invoke(this, session);
        };
        
        _connectionManager.ClientDisconnected += (s, session) =>
        {
            _heartbeatManager.UnregisterSession(session.SessionId);
            ClientDisconnected?.Invoke(this, session);
        };
    }
    
    private async void OnSessionTimeout(string sessionId)
    {
        var session = _connectionManager.GetSession(sessionId);
        if (session != null && session.IsConnected)
        {
            Log.Warning("세션 타임아웃으로 연결 종료: {SessionId}", sessionId);
            await session.DisconnectAsync();
        }
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
        
        Log.Information("게임 TCP 서버 시작: {Address}:{Port}", address, port);
        Log.Information("최대 연결: {MaxConnections}, 패킷 워커: {WorkerCount}", 
            _connectionManager.MaxConnections, _packetProcessor.WorkerCount);
        
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
        _heartbeatManager.Dispose();
        
        Log.Information("게임 TCP 서버 중지");
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
                // 리스너가 종료된 경우 (정상 종료)
                Log.Debug("TCP 리스너 종료됨");
                break;
            }
            catch (SocketException ex)
            {
                Log.Warning(ex, "클라이언트 수락 소켓 오류: {SocketErrorCode} - {Message}", 
                    ex.SocketErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "클라이언트 수락 오류: {Message}", ex.Message);
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
                Log.Warning("연결 수 초과: 세션 {SessionId} 거부", session.SessionId);
                await session.DisconnectAsync();
                return;
            }
            
            Log.Information("클라이언트 연결: 세션 {SessionId} (현재 연결: {ActiveCount})", 
                session.SessionId, _connectionManager.ActiveSessionCount);
            
            // 세션이 종료될 때까지 대기하며 하트비트 체크
            while (session.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                // 하트비트 업데이트 (패킷 수신 시 자동 업데이트됨)
                if (session is GameClientSession gameSession)
                {
                    _heartbeatManager.UpdateActivity(gameSession.SessionId);
                }
                
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (SocketException ex)
        {
            Log.Warning(ex, "클라이언트 처리 소켓 오류: {SocketErrorCode} - {Message}", 
                ex.SocketErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "클라이언트 처리 오류: {Message}", ex.Message);
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
        Log.Debug("패킷 수신: 세션 {SessionId}, 타입: {PacketId}", 
            context.Session.SessionId, context.Packet.PacketId);
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

