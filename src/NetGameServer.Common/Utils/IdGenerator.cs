using System.Security.Cryptography;

namespace NetGameServer.Common.Utils;

/// <summary>
/// 고유 ID 생성기
/// </summary>
public static class IdGenerator
{
    private static readonly object _lock = new();
    private static long _sequence = 0;
    private static long _lastTimestamp = 0;
    
    // Snowflake ID 생성에 사용 (서버 ID는 설정 가능)
    private const int ServerIdBits = 5;
    private const int SequenceBits = 12;
    private const long MaxServerId = (1L << ServerIdBits) - 1;
    private const long MaxSequence = (1L << SequenceBits) - 1;
    
    private static long _serverId = 1; // 기본 서버 ID
    
    /// <summary>
    /// 서버 ID 설정 (Snowflake ID 생성용)
    /// </summary>
    public static void SetServerId(long serverId)
    {
        if (serverId < 0 || serverId > MaxServerId)
            throw new ArgumentOutOfRangeException(nameof(serverId), $"Server ID must be between 0 and {MaxServerId}");
        
        _serverId = serverId;
    }
    
    /// <summary>
    /// UUID 생성 (GUID)
    /// </summary>
    public static string GenerateUuid()
    {
        return Guid.NewGuid().ToString();
    }
    
    /// <summary>
    /// UUID 생성 (하이픈 없이)
    /// </summary>
    public static string GenerateUuidWithoutHyphens()
    {
        return Guid.NewGuid().ToString("N");
    }
    
    /// <summary>
    /// Snowflake ID 생성 (Twitter Snowflake 알고리즘)
    /// 64비트 ID: [타임스탬프(41비트)][서버ID(5비트)][시퀀스(12비트)]
    /// </summary>
    public static long GenerateSnowflakeId()
    {
        lock (_lock)
        {
            var timestamp = GetCurrentTimestamp();
            
            // 같은 밀리초 내에 여러 ID가 요청된 경우
            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;
                
                // 시퀀스가 오버플로우된 경우 다음 밀리초까지 대기
                if (_sequence == 0)
                {
                    timestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }
            
            _lastTimestamp = timestamp;
            
            // ID 조합: [타임스탬프(41비트)][서버ID(5비트)][시퀀스(12비트)]
            return (timestamp << (ServerIdBits + SequenceBits))
                   | (_serverId << SequenceBits)
                   | _sequence;
        }
    }
    
    /// <summary>
    /// 현재 타임스탬프 (밀리초, 1970-01-01 기준)
    /// </summary>
    private static long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
    
    /// <summary>
    /// 다음 밀리초까지 대기
    /// </summary>
    private static long WaitNextMillis(long lastTimestamp)
    {
        long timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }
    
    /// <summary>
    /// Snowflake ID에서 타임스탬프 추출
    /// </summary>
    public static DateTimeOffset ExtractTimestampFromSnowflake(long snowflakeId)
    {
        var timestamp = (snowflakeId >> (ServerIdBits + SequenceBits)) + 62135596800000L; // 1970-01-01 기준
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
    }
    
    /// <summary>
    /// Snowflake ID에서 서버 ID 추출
    /// </summary>
    public static long ExtractServerIdFromSnowflake(long snowflakeId)
    {
        return (snowflakeId >> SequenceBits) & MaxServerId;
    }
    
    /// <summary>
    /// Snowflake ID에서 시퀀스 추출
    /// </summary>
    public static long ExtractSequenceFromSnowflake(long snowflakeId)
    {
        return snowflakeId & MaxSequence;
    }
    
    /// <summary>
    /// 고유 세션 ID 생성 (Base64 URL-safe)
    /// </summary>
    public static string GenerateSessionId()
    {
        return RandomHelper.GenerateToken(32);
    }
    
    /// <summary>
    /// 고유 토큰 생성 (인증용)
    /// </summary>
    public static string GenerateAuthToken()
    {
        return RandomHelper.GenerateToken(64);
    }
    
    /// <summary>
    /// 짧은 고유 ID 생성 (8자리)
    /// </summary>
    public static string GenerateShortId()
    {
        return RandomHelper.GenerateString(8);
    }
    
    /// <summary>
    /// 숫자 기반 고유 ID 생성
    /// </summary>
    public static string GenerateNumericId(int length = 10)
    {
        return RandomHelper.GenerateNumericString(length);
    }
}
