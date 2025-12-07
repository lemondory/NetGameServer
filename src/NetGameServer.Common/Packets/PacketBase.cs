namespace NetGameServer.Common.Packets;

// 모든 패킷의 기본 클래스
public abstract class PacketBase
{
    public abstract ushort PacketId { get; }
    public abstract byte[] Serialize();
    public abstract void Deserialize(byte[] data);
}

