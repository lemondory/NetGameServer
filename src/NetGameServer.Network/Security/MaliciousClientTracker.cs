using System.Collections.Concurrent;
using System.Net;
using Serilog;

namespace NetGameServer.Network.Security;

/// <summary>
/// 악의적 클라이언트 추적 및 차단 (잘못된 패킷 반복 전송 시)
/// </summary>
public class MaliciousClientTracker
{
    private readonly ConcurrentDictionary<string, int> _invalidCountBySession = new();
    private readonly ConcurrentDictionary<string, IPAddress> _sessionToIp = new();
    private readonly ConcurrentDictionary<IPAddress, BlockEntry> _blockedIps = new();
    private readonly int _threshold;
    private readonly IBlockedListRepository _repository;
    private readonly SemaphoreSlim _persistLock = new(1, 1);

    public int InvalidPacketThreshold => _threshold;

    /// <summary>
    /// 파일 기반 (기본값)
    /// </summary>
    public MaliciousClientTracker(
        int invalidPacketThreshold = 10,
        string? blockListPath = null)
        : this(invalidPacketThreshold, new FileBlockedListRepository(blockListPath))
    {
    }

    /// <summary>
    /// 저장소 주입 (DB 등)
    /// </summary>
    public MaliciousClientTracker(
        int invalidPacketThreshold,
        IBlockedListRepository repository)
    {
        _threshold = invalidPacketThreshold;
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        LoadBlockedListAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 세션 등록 (연결 시 IP 기록)
    /// </summary>
    public void RegisterSession(string sessionId, IPEndPoint? remoteEndPoint)
    {
        if (remoteEndPoint != null)
            _sessionToIp.TryAdd(sessionId, remoteEndPoint.Address);
    }

    /// <summary>
    /// 잘못된 패킷 기록. 임계치 초과 시 true 반환 (차단 필요)
    /// </summary>
    public bool RecordInvalidPacket(string sessionId, IPEndPoint? remoteEndPoint, out BlockEntry? blockEntry)
    {
        blockEntry = null;
        if (remoteEndPoint != null)
            _sessionToIp.AddOrUpdate(sessionId, remoteEndPoint.Address, (_, _) => remoteEndPoint.Address);

        var count = _invalidCountBySession.AddOrUpdate(sessionId, 1, (_, c) => c + 1);

        if (count >= _threshold)
        {
            var ip = _sessionToIp.TryGetValue(sessionId, out var addr) ? addr : remoteEndPoint?.Address;
            if (ip != null)
            {
                blockEntry = new BlockEntry(ip.ToString(), DateTime.UtcNow, count);
                _blockedIps.TryAdd(ip, blockEntry);
                Log.Warning("악의적 클라이언트 차단: IP={Ip}, SessionId={SessionId}, InvalidCount={Count}",
                    ip, sessionId, count);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 세션 해제 시 카운트 정리
    /// </summary>
    public void UnregisterSession(string sessionId)
    {
        _invalidCountBySession.TryRemove(sessionId, out _);
        _sessionToIp.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// IP가 차단 목록에 있는지 확인 (연결 수락 전 검사)
    /// </summary>
    public bool IsBlocked(IPEndPoint? remoteEndPoint)
    {
        return remoteEndPoint != null && _blockedIps.ContainsKey(remoteEndPoint.Address);
    }

    /// <summary>
    /// 차단 목록 저장 (서버 종료 시 호출)
    /// </summary>
    public async Task SaveBlockedListAsync(CancellationToken cancellationToken = default)
    {
        await _persistLock.WaitAsync(cancellationToken);
        try
        {
            var entries = _blockedIps.Values.ToList();
            await _repository.SaveAsync(entries, cancellationToken);
        }
        finally
        {
            _persistLock.Release();
        }
    }

    /// <summary>
    /// 차단 목록 저장 (동기, 서버 종료 시 호출)
    /// </summary>
    public void SaveBlockedList() => SaveBlockedListAsync().GetAwaiter().GetResult();

    private async Task LoadBlockedListAsync()
    {
        await _persistLock.WaitAsync();
        try
        {
            var entries = await _repository.LoadAsync();
            foreach (var e in entries)
            {
                if (IPAddress.TryParse(e.Ip, out var ip))
                    _blockedIps.TryAdd(ip, e);
            }
        }
        finally
        {
            _persistLock.Release();
        }
    }
}
