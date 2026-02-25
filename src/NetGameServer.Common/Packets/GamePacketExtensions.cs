using Google.Protobuf;
using NetGameServer.Common.Packets.Proto;

namespace NetGameServer.Common.Packets;

/// <summary>
/// GamePacket 확장 메서드 (역직렬화, 우선순위)
/// 직렬화는 Protobuf 표준 packet.ToByteArray() 사용
/// </summary>
public static class GamePacketExtensions
{
    /// <summary>
    /// 바이트 배열에서 GamePacket 역직렬화 (TryParse 스타일, 예외 최소화)
    /// </summary>
    /// <param name="data">패킷 바이트</param>
    /// <param name="packet">성공 시 역직렬화된 패킷, 실패 시 null</param>
    /// <returns>성공 여부</returns>
    public static bool TryToGamePacket(this byte[]? data, out GamePacket? packet)
    {
        packet = null;
        if (data == null || data.Length == 0)
            return false;

        try
        {
            packet = GamePacket.Parser.ParseFrom(data);
            return true;
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }
    }

    /// <summary>
    /// 패킷 처리 우선순위 반환
    /// </summary>
    public static int GetPriority(this GamePacket packet)
    {
        return packet.PayloadCase switch
        {
            GamePacket.PayloadOneofCase.MoveRequest => 100,
            GamePacket.PayloadOneofCase.ObjectUpdate => 100,
            GamePacket.PayloadOneofCase.ObjectSpawn => 50,
            GamePacket.PayloadOneofCase.ObjectDespawn => 50,
            GamePacket.PayloadOneofCase.LoginRequest => 50,
            GamePacket.PayloadOneofCase.LoginResponse => 50,
            GamePacket.PayloadOneofCase.ReconnectRequest => 50,
            GamePacket.PayloadOneofCase.ReconnectResponse => 50,
            GamePacket.PayloadOneofCase.ObjectSnapshot => 0,
            GamePacket.PayloadOneofCase.Heartbeat => -50,
            _ => 0
        };
    }
}
