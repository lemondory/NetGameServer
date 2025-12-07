using System.Net;
using NetGameServer.Auth;
using NetGameServer.Common.Packets;
using NetGameServer.Game.Services;
using NetGameServer.Game.Spatial;
using NetGameServer.Network.Sessions;
using NetGameServer.Network.Servers;
using Serilog;

namespace NetGameServer;

class Program
{
    private static GameTcpServer? _tcpServer;
    private static IAuthService? _authService;
    private static GameService? _gameService;
    
    static async Task Main(string[] args)
    {
        // Serilog 로거 초기화
        Log.Logger = LoggerConfig.CreateLogger();
        
        try
        {
            // 공간 분할 테스트 옵션
            if (args.Length > 0 && args[0] == "--test-spatial")
            {
                Log.Information("=== 공간 분할 테스트 모드 ===");
                SpatialPartitionTests.RunBasicTests();
                SpatialPartitionTests.RunPerformanceTest(1000);
                return;
            }
            
            Log.Information("=== NetGameServer 시작 ===");
        
            _authService = new AuthService();
            await _authService.RegisterAsync("testuser", "testpass");
            Log.Information("테스트 사용자 등록: testuser / testpass");
            
            _gameService = new GameService(_authService);
            
            _tcpServer = new GameTcpServer(maxConnections: 10000, packetWorkerCount: 4);
            _tcpServer.ClientConnected += OnClientConnected;
            _tcpServer.ClientDisconnected += OnClientDisconnected;
            
            _tcpServer.PacketHandler = (context) =>
            {
                _gameService?.HandlePacket(context);
            };
            
            var port = 8888;
            await _tcpServer.StartAsync(IPAddress.Any, port);
            
            Log.Information("서버가 포트 {Port}에서 실행 중입니다...", port);
            Log.Information("종료하려면 'q'를 입력하세요.");
        
        // 상태 모니터링
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(5000);
                if (_tcpServer != null)
                {
                    Log.Information("[상태] 연결: {ActiveSessions}, 대기 패킷: {QueuedPackets}, 사용 가능 연결: {AvailableConnections}",
                        _tcpServer.ActiveSessionCount,
                        _tcpServer.QueuedPacketCount,
                        _tcpServer.AvailableConnections);
                }
            }
        });
        
        // 오브젝트 동기화 루프
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    if (_gameService != null)
                    {
                        await _gameService.SyncObjectsAsync();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "동기화 오류 발생");
                }
                
                await Task.Delay(100); // 100ms마다 동기화
            }
        });
        
        while (true)
        {
            var input = Console.ReadLine();
            if (input?.ToLower() == "q")
            {
                break;
            }
        }
        
            await _tcpServer.StopAsync();
            _gameService?.Dispose();
            Log.Information("서버가 종료되었습니다.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "서버 실행 중 치명적 오류 발생");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    private static void OnClientConnected(object? sender, IClientSession session)
    {
        Log.Information("[서버] 클라이언트 연결됨: {SessionId}", session.SessionId);
    }
    
    private static void OnClientDisconnected(object? sender, IClientSession session)
    {
        Log.Information("[서버] 클라이언트 연결 해제됨: {SessionId}", session.SessionId);
        
        if (_gameService != null)
        {
            _ = _gameService.EndGameAsync(session);
        }
    }
}
