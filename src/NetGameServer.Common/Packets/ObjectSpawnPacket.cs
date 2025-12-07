namespace NetGameServer.Common.Packets;

// 오브젝트 스폰 패킷
public class ObjectSpawnPacket : PacketBase
{
    public override ushort PacketId => (ushort)PacketType.ObjectSpawn;
    
    public uint ObjectId { get; set; }
    public byte ObjectType { get; set; } // GameObjectType enum value
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    // 추가 속성 (타입별로 다를 수 있음)
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Level { get; set; }
    
    public override byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(PacketId);
        writer.Write(ObjectId);
        writer.Write(ObjectType);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Z);
        writer.Write(Hp);
        writer.Write(MaxHp);
        writer.Write(Level);
        
        return ms.ToArray();
    }
    
    public override void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var packetId = reader.ReadUInt16();
        ObjectId = reader.ReadUInt32();
        ObjectType = reader.ReadByte();
        X = reader.ReadSingle();
        Y = reader.ReadSingle();
        Z = reader.ReadSingle();
        Hp = reader.ReadInt32();
        MaxHp = reader.ReadInt32();
        Level = reader.ReadInt32();
    }
}

