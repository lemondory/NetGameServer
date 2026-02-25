namespace NetGameServer.Network.Security;

/// <summary>
/// 차단된 IP 정보
/// </summary>
public record BlockEntry(string Ip, DateTime BlockedAt, int InvalidCount);
