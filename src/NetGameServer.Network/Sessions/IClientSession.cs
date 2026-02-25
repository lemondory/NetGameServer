using NetGameServer.Common.Packets.Proto;

namespace NetGameServer.Network.Sessions;

/// <summary>
/// 클라이언트 세션 인터페이스
/// </summary>
public interface IClientSession
{
    string SessionId { get; }
    bool IsConnected { get; }
    Task SendPacketAsync(GamePacket packet);
    Task DisconnectAsync();
    event EventHandler<GamePacket>? PacketReceived;
    
    /// <summary>
    /// 연결 종료 이벤트
    /// </summary>
    event EventHandler? Disconnected;
}

