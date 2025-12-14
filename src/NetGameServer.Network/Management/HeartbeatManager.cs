using System.Collections.Concurrent;
using NetGameServer.Network.Sessions;
using Serilog;

namespace NetGameServer.Network.Management;

/// <summary>
/// 하트비트 관리자 - 연결 타임아웃 감지
/// </summary>
public class HeartbeatManager : IDisposable
{
    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new();
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _timeout;
    private readonly Timer? _checkTimer;
    private readonly Action<string>? _onTimeout;
    private bool _disposed = false;
    
    /// <summary>
    /// 하트비트 관리자 생성
    /// </summary>
    /// <param name="heartbeatInterval">하트비트 간격 (기본 30초)</param>
    /// <param name="timeout">타임아웃 시간 (기본 90초)</param>
    /// <param name="onTimeout">타임아웃 발생 시 호출할 액션</param>
    public HeartbeatManager(
        TimeSpan? heartbeatInterval = null, 
        TimeSpan? timeout = null,
        Action<string>? onTimeout = null)
    {
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30);
        _timeout = timeout ?? TimeSpan.FromSeconds(90);
        _onTimeout = onTimeout;
        
        // 주기적으로 타임아웃 체크 (하트비트 간격마다)
        _checkTimer = new Timer(CheckTimeouts, null, _heartbeatInterval, _heartbeatInterval);
    }
    
    /// <summary>
    /// 세션의 마지막 활동 시간 업데이트
    /// </summary>
    public void UpdateActivity(string sessionId)
    {
        _lastActivity.AddOrUpdate(sessionId, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
    }
    
    /// <summary>
    /// 세션 등록
    /// </summary>
    public void RegisterSession(string sessionId)
    {
        _lastActivity.TryAdd(sessionId, DateTime.UtcNow);
        Log.Debug("하트비트 등록: 세션 {SessionId}", sessionId);
    }
    
    /// <summary>
    /// 세션 제거
    /// </summary>
    public void UnregisterSession(string sessionId)
    {
        _lastActivity.TryRemove(sessionId, out _);
        Log.Debug("하트비트 제거: 세션 {SessionId}", sessionId);
    }
    
    /// <summary>
    /// 타임아웃된 세션 목록 조회
    /// </summary>
    public List<string> GetTimedOutSessions()
    {
        var now = DateTime.UtcNow;
        return _lastActivity
            .Where(kvp => now - kvp.Value > _timeout)
            .Select(kvp => kvp.Key)
            .ToList();
    }
    
    private void CheckTimeouts(object? state)
    {
        if (_disposed)
            return;
            
        var timedOutSessions = GetTimedOutSessions();
        
        foreach (var sessionId in timedOutSessions)
        {
            Log.Warning("세션 타임아웃: {SessionId} (마지막 활동: {LastActivity}초 전)", 
                sessionId, (DateTime.UtcNow - _lastActivity[sessionId]).TotalSeconds);
            
            _onTimeout?.Invoke(sessionId);
            _lastActivity.TryRemove(sessionId, out _);
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        _checkTimer?.Dispose();
        _lastActivity.Clear();
    }
}

