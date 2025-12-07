using Serilog;
using Serilog.Events;

namespace NetGameServer.TestClient;

// 클라이언트용 Serilog 로거 설정
public static class ClientLoggerConfig
{
    public static ILogger CreateLogger()
    {
        // logs 디렉토리 생성
        var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }
        
        // 로그 파일 경로 (날짜별로 분리)
        var logFile = Path.Combine(logsDir, "client-.log");
        
        return new LoggerConfiguration()
            .MinimumLevel.Debug() // 디버그 레벨까지 로그 출력 (패킷 수신 확인용)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, // 7일치 로그 보관
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1)) // 1초마다 디스크에 플러시
            .CreateLogger();
    }
}

