namespace NetGameServer.Network.Security;

/// <summary>
/// 차단 목록 저장소 인터페이스 (파일/DB 등)
/// </summary>
public interface IBlockedListRepository
{
    /// <summary>
    /// 차단 목록 저장
    /// </summary>
    Task SaveAsync(IReadOnlyCollection<BlockEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// 차단 목록 로드
    /// </summary>
    Task<IReadOnlyList<BlockEntry>> LoadAsync(CancellationToken cancellationToken = default);
}
