using NetGameServer.Common.Packets;

namespace NetGameServer.Auth;

/// <summary>
/// 인증 서비스 인터페이스
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 로그인 처리
    /// </summary>
    Task<LoginResponsePacket> LoginAsync(LoginRequestPacket request);
    
    /// <summary>
    /// 토큰 검증
    /// </summary>
    Task<bool> ValidateTokenAsync(string token);
    
    /// <summary>
    /// 사용자 등록
    /// </summary>
    Task<bool> RegisterAsync(string username, string password);
}

