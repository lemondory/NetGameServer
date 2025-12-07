using System.Collections.Concurrent;
using System.Net;

using NetGameServer.Network.Sessions;

namespace NetGameServer.Network.Management;

/// <summary>
/// 연결 관리자 - 모든 클라이언트 세션 관리
/// </summary>
public class ConnectionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, IClientSession> _sessions = new();
    private readonly SemaphoreSlim _connectionLimiter;
    public int MaxConnections { get; }
    
    public event EventHandler<IClientSession>? ClientConnected;
    public event EventHandler<IClientSession>? ClientDisconnected;
    
    public ConnectionManager(int maxConnections = 10000)
    {
        MaxConnections = maxConnections;
        _connectionLimiter = new SemaphoreSlim(maxConnections, maxConnections);
    }
    
    /// <summary>
    /// 세션 추가
    /// </summary>
    public bool TryAddSession(IClientSession session)
    {
        if (!_connectionLimiter.Wait(0))
        {
            return false; // 연결 수 초과
        }
        
        if (_sessions.TryAdd(session.SessionId, session))
        {
            session.Disconnected += OnSessionDisconnected;
            ClientConnected?.Invoke(this, session);
            return true;
        }
        
        _connectionLimiter.Release();
        return false;
    }
    
    /// <summary>
    /// 세션 제거
    /// </summary>
    public bool TryRemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            _connectionLimiter.Release();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 세션 조회
    /// </summary>
    public IClientSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }
    
    /// <summary>
    /// 모든 활성 세션 조회
    /// </summary>
    public IEnumerable<IClientSession> GetAllSessions()
    {
        return _sessions.Values.Where(s => s.IsConnected);
    }
    
    /// <summary>
    /// 활성 세션 수
    /// </summary>
    public int ActiveSessionCount => _sessions.Count(s => s.Value.IsConnected);
    
    /// <summary>
    /// 사용 가능한 연결 슬롯 수
    /// </summary>
    public int AvailableConnections => _connectionLimiter.CurrentCount;
    
    private void OnSessionDisconnected(object? sender, EventArgs e)
    {
        if (sender is IClientSession session)
        {
            TryRemoveSession(session.SessionId);
            ClientDisconnected?.Invoke(this, session);
        }
    }
    
    public void Dispose()
    {
        var sessions = _sessions.Values.ToList();
        foreach (var session in sessions)
        {
            session.DisconnectAsync().Wait();
        }
        _sessions.Clear();
        _connectionLimiter.Dispose();
    }
}

