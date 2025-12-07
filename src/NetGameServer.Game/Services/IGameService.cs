using NetGameServer.Network.Sessions;

namespace NetGameServer.Game.Services;

/// <summary>
/// 게임 서비스 인터페이스
/// </summary>
public interface IGameService
{
    /// <summary>
    /// 게임 시작
    /// </summary>
    Task StartGameAsync(IClientSession session);
    
    /// <summary>
    /// 게임 액션 처리
    /// </summary>
    Task ProcessGameActionAsync(IClientSession session, string action);
    
    /// <summary>
    /// 게임 종료
    /// </summary>
    Task EndGameAsync(IClientSession session);
}

