using System.Net;
using NetGameServer.Common.Packets;
using NetGameServer.TestClient;
using Serilog;

namespace NetGameServer.TestClient;

class Program
{
    static async Task Main(string[] args)
    {
        // Serilog 로거 초기화
        Log.Logger = ClientLoggerConfig.CreateLogger();
        
        try
        {
            Log.Information("=== NetGameServer 테스트 클라이언트 ===");
            Log.Information("");
        
            // 테스트 시나리오 반복 횟수 입력
            int scenarioCount = GetScenarioCount();
            Log.Information("");
            
            // 서버 주소 및 포트
            var serverAddress = IPAddress.Loopback; // localhost
            var serverPort = 8888;
            
            Log.Information("서버 주소: {Address}:{Port}", serverAddress, serverPort);
            Log.Information("테스트 시나리오 반복 횟수: {Count}", scenarioCount);
            Log.Information("");
            
            // 테스트 시나리오 선택
            int scenarioType = GetScenarioType();
            Log.Information("");
            
            // 각 시나리오 실행
            for (int i = 0; i < scenarioCount; i++)
            {
                Log.Information("========== 시나리오 {Current}/{Total} ==========", i + 1, scenarioCount);
                
                if (scenarioType == 1)
                {
                    await RunTestScenarioAsync(serverAddress, serverPort, i + 1);
                }
                else if (scenarioType == 2)
                {
                    await RunReconnectTestScenarioAsync(serverAddress, serverPort, i + 1);
                }
                
                Log.Information("");
                
                // 시나리오 간 대기 (선택적)
                if (i < scenarioCount - 1)
                {
                    await Task.Delay(1000);
                }
            }
            
            Log.Information("=== 모든 테스트 완료 ===");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "테스트 클라이언트 실행 중 치명적 오류 발생");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    /// <summary>
    /// 시나리오 반복 횟수 입력
    /// </summary>
    static int GetScenarioCount()
    {
        while (true)
        {
            Console.Write("테스트 시나리오를 몇 번 반복할까요? (기본값: 1): ");
            var input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
            {
                return 1;
            }
            
            if (int.TryParse(input, out var count) && count > 0)
            {
                return count;
            }
            
            Log.Warning("올바른 숫자를 입력해주세요.");
        }
    }
    
    /// <summary>
    /// 테스트 시나리오 타입 선택
    /// </summary>
    static int GetScenarioType()
    {
        while (true)
        {
            Console.WriteLine("테스트 시나리오를 선택하세요:");
            Console.WriteLine("  1. 기본 시나리오 (연결 -> 로그인 -> 이동 -> 종료)");
            Console.WriteLine("  2. 재연결 시나리오 (연결 -> 로그인 -> 이동 -> 연결 끊김 -> 재연결 -> 이동 -> 종료)");
            Console.Write("선택 (기본값: 1): ");
            var input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
            {
                return 1;
            }
            
            if (int.TryParse(input, out var type) && (type == 1 || type == 2))
            {
                return type;
            }
            
            Log.Warning("1 또는 2를 입력해주세요.");
        }
    }
    
    /// <summary>
    /// 테스트 시나리오 실행
    /// </summary>
    static async Task RunTestScenarioAsync(IPAddress address, int port, int scenarioNumber)
    {
        TestClient? client = null;
        
        try
        {
            // 1. 클라이언트 생성 및 연결
            client = new TestClient();
            Log.Information("[시나리오 {ScenarioNumber}] 1. 연결 시도...", scenarioNumber);
            
            if (!await client.ConnectAsync(address, port))
            {
                Log.Warning("[시나리오 {ScenarioNumber}] 연결 실패", scenarioNumber);
                return;
            }
            
            await Task.Delay(100); // 연결 안정화 대기
            
            // 2. 로그인
            Log.Information("[시나리오 {ScenarioNumber}] 2. 로그인 시도...", scenarioNumber);
            var loginRequest = new LoginRequestPacket
            {
                Username = "testuser",
                Password = "testpass"
            };
            
            await client.SendPacketAsync(loginRequest);
            
            // 로그인 응답 대기 (특정 패킷 타입만 받기)
            var loginResponse = await client.ReceivePacketAsync(TimeSpan.FromSeconds(5), (ushort)PacketType.LoginResponse);
            if (loginResponse is LoginResponsePacket response && response.Success)
            {
                Log.Information("[시나리오 {ScenarioNumber}] 로그인 성공", scenarioNumber);
                // 인증 토큰 저장 (재연결용)
                if (!string.IsNullOrEmpty(response.Token))
                {
                    client.SetAuthToken(response.Token, loginRequest.Username);
                }
            }
            else
            {
                Log.Warning("[시나리오 {ScenarioNumber}] 로그인 실패", scenarioNumber);
                return;
            }
            
            await Task.Delay(200); // 게임 시작 대기
            
            // 3. 랜덤 이동 10회
            Log.Information("[시나리오 {ScenarioNumber}] 3. 랜덤 이동 시작 (10회)...", scenarioNumber);
            var random = new Random();
            float currentX = 0, currentY = 0, currentZ = 0;
            
            for (int i = 0; i < 10; i++)
            {
                // 랜덤 목표 위치 생성 (-50 ~ 50 범위)
                float targetX = random.NextSingle() * 100.0f - 50.0f;
                float targetY = 0.0f;
                float targetZ = random.NextSingle() * 100.0f - 50.0f;
                
                var moveRequest = new MoveRequestPacket
                {
                    TargetX = targetX,
                    TargetY = targetY,
                    TargetZ = targetZ
                };
                
                await client.SendPacketAsync(moveRequest);
                Log.Information("[시나리오 {ScenarioNumber}]   이동 {MoveCount}/10: ({OldX:F2}, {OldY:F2}, {OldZ:F2}) → ({NewX:F2}, {NewY:F2}, {NewZ:F2})",
                    scenarioNumber, i + 1, currentX, currentY, currentZ, targetX, targetY, targetZ);
                
                currentX = targetX;
                currentY = targetY;
                currentZ = targetZ;
                
                // 이동 간 대기 (이동 시간 시뮬레이션)
                await Task.Delay(500);
            }
            
            Log.Information("[시나리오 {ScenarioNumber}] 4. 랜덤 이동 완료 (10회)", scenarioNumber);
            
            // 5. 연결 종료
            Log.Information("[시나리오 {ScenarioNumber}] 5. 연결 종료...", scenarioNumber);
            await client.DisconnectAsync();
            
            Log.Information("[시나리오 {ScenarioNumber}] ✓ 완료", scenarioNumber);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[시나리오 {ScenarioNumber}] ✗ 오류 발생", scenarioNumber);
        }
        finally
        {
            client?.Dispose();
        }
    }
    
    /// <summary>
    /// 재연결 테스트 시나리오 실행
    /// </summary>
    static async Task RunReconnectTestScenarioAsync(IPAddress address, int port, int scenarioNumber)
    {
        TestClient? client = null;
        
        try
        {
            // 1. 클라이언트 생성 및 연결
            client = new TestClient();
            Log.Information("[시나리오 {ScenarioNumber}] 1. 연결 시도...", scenarioNumber);
            
            if (!await client.ConnectAsync(address, port))
            {
                Log.Warning("[시나리오 {ScenarioNumber}] 연결 실패", scenarioNumber);
                return;
            }
            
            await Task.Delay(100); // 연결 안정화 대기
            
            // 2. 로그인
            Log.Information("[시나리오 {ScenarioNumber}] 2. 로그인 시도...", scenarioNumber);
            var loginRequest = new LoginRequestPacket
            {
                Username = "testuser",
                Password = "testpass"
            };
            
            await client.SendPacketAsync(loginRequest);
            
            // 로그인 응답 대기
            var loginResponse = await client.ReceivePacketAsync(TimeSpan.FromSeconds(5), (ushort)PacketType.LoginResponse);
            if (loginResponse is LoginResponsePacket response && response.Success)
            {
                Log.Information("[시나리오 {ScenarioNumber}] 로그인 성공", scenarioNumber);
                // 인증 토큰 저장 (재연결용)
                if (!string.IsNullOrEmpty(response.Token))
                {
                    client.SetAuthToken(response.Token, loginRequest.Username);
                }
            }
            else
            {
                Log.Warning("[시나리오 {ScenarioNumber}] 로그인 실패", scenarioNumber);
                return;
            }
            
            await Task.Delay(200); // 게임 시작 대기
            
            // 3. 랜덤 이동 5회
            Log.Information("[시나리오 {ScenarioNumber}] 3. 랜덤 이동 시작 (5회)...", scenarioNumber);
            var random = new Random();
            float currentX = 0, currentY = 0, currentZ = 0;
            
            for (int i = 0; i < 5; i++)
            {
                float targetX = random.NextSingle() * 100.0f - 50.0f;
                float targetY = 0.0f;
                float targetZ = random.NextSingle() * 100.0f - 50.0f;
                
                var moveRequest = new MoveRequestPacket
                {
                    TargetX = targetX,
                    TargetY = targetY,
                    TargetZ = targetZ
                };
                
                await client.SendPacketAsync(moveRequest);
                Log.Information("[시나리오 {ScenarioNumber}]   이동 {MoveCount}/5: ({OldX:F2}, {OldY:F2}, {OldZ:F2}) → ({NewX:F2}, {NewY:F2}, {NewZ:F2})",
                    scenarioNumber, i + 1, currentX, currentY, currentZ, targetX, targetY, targetZ);
                
                currentX = targetX;
                currentY = targetY;
                currentZ = targetZ;
                
                await Task.Delay(500);
            }
            
            Log.Information("[시나리오 {ScenarioNumber}] 4. 랜덤 이동 완료 (5회)", scenarioNumber);
            
            // 5. 연결 강제 종료 (재연결 테스트)
            Log.Information("[시나리오 {ScenarioNumber}] 5. 연결 강제 종료 (재연결 테스트)...", scenarioNumber);
            await client.DisconnectAsync();
            await Task.Delay(1000); // 연결 종료 대기
            
            // 6. 재연결 시도
            Log.Information("[시나리오 {ScenarioNumber}] 6. 재연결 시도...", scenarioNumber);
            if (!await client.ReconnectAsync(address, port, maxRetries: 3))
            {
                Log.Warning("[시나리오 {ScenarioNumber}] 재연결 실패", scenarioNumber);
                return;
            }
            
            await Task.Delay(200); // 재연결 안정화 대기
            
            // 7. 재연결 후 랜덤 이동 5회
            Log.Information("[시나리오 {ScenarioNumber}] 7. 재연결 후 랜덤 이동 시작 (5회)...", scenarioNumber);
            
            for (int i = 0; i < 5; i++)
            {
                float targetX = random.NextSingle() * 100.0f - 50.0f;
                float targetY = 0.0f;
                float targetZ = random.NextSingle() * 100.0f - 50.0f;
                
                var moveRequest = new MoveRequestPacket
                {
                    TargetX = targetX,
                    TargetY = targetY,
                    TargetZ = targetZ
                };
                
                await client.SendPacketAsync(moveRequest);
                Log.Information("[시나리오 {ScenarioNumber}]   이동 {MoveCount}/5: ({OldX:F2}, {OldY:F2}, {OldZ:F2}) → ({NewX:F2}, {NewY:F2}, {NewZ:F2})",
                    scenarioNumber, i + 1, currentX, currentY, currentZ, targetX, targetY, targetZ);
                
                currentX = targetX;
                currentY = targetY;
                currentZ = targetZ;
                
                await Task.Delay(500);
            }
            
            Log.Information("[시나리오 {ScenarioNumber}] 8. 재연결 후 랜덤 이동 완료 (5회)", scenarioNumber);
            
            // 9. 연결 종료
            Log.Information("[시나리오 {ScenarioNumber}] 9. 연결 종료...", scenarioNumber);
            await client.DisconnectAsync();
            
            Log.Information("[시나리오 {ScenarioNumber}] ✓ 완료", scenarioNumber);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[시나리오 {ScenarioNumber}] ✗ 오류 발생", scenarioNumber);
        }
        finally
        {
            client?.Dispose();
        }
    }
}
