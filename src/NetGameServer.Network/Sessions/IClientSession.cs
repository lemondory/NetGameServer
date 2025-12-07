using NetGameServer.Common.Packets;

namespace NetGameServer.Network.Sessions;

/// <summary>
/// 클라이언트 세션 인터페이스
/// </summary>
public interface IClientSession
{
    /// <summary>
    /// 세션 ID
    /// </summary>
    string SessionId { get; }
    
    /// <summary>
    /// 연결 상태
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// 패킷 전송
    /// </summary>
    Task SendPacketAsync(PacketBase packet);
    
    /// <summary>
    /// 연결 종료
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// 패킷 수신 이벤트
    /// </summary>
    event EventHandler<PacketBase>? PacketReceived;
    
    /// <summary>
    /// 연결 종료 이벤트
    /// </summary>
    event EventHandler? Disconnected;
}

