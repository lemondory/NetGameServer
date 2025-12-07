namespace NetGameServer.Common.Packets;

// 오브젝트 제거 패킷
public class ObjectDespawnPacket : PacketBase
{
    public override ushort PacketId => (ushort)PacketType.ObjectDespawn;
    
    public uint ObjectId { get; set; }
    
    public override byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(PacketId);
        writer.Write(ObjectId);
        
        return ms.ToArray();
    }
    
    public override void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var packetId = reader.ReadUInt16();
        ObjectId = reader.ReadUInt32();
    }
}

