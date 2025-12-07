namespace NetGameServer.Common.Packets;

// 오브젝트 상태 업데이트 패킷 (Delta Compression)
public class ObjectUpdatePacket : PacketBase
{
    public override ushort PacketId => (ushort)PacketType.ObjectUpdate;
    
    public uint ObjectId { get; set; }
    
    // 변경된 속성만 포함 (플래그로 표시)
    public bool HasPosition { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    public bool HasHp { get; set; }
    public int Hp { get; set; }
    
    public bool HasLevel { get; set; }
    public int Level { get; set; }
    
    // 추가 속성 플래그 (확장 가능)
    public byte Flags { get; set; }
    
    public override byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(PacketId);
        writer.Write(ObjectId);
        writer.Write(Flags);
        
        if (HasPosition)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
        }
        
        if (HasHp)
        {
            writer.Write(Hp);
        }
        
        if (HasLevel)
        {
            writer.Write(Level);
        }
        
        return ms.ToArray();
    }
    
    public override void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var packetId = reader.ReadUInt16();
        ObjectId = reader.ReadUInt32();
        Flags = reader.ReadByte();
        
        HasPosition = (Flags & 0x01) != 0;
        HasHp = (Flags & 0x02) != 0;
        HasLevel = (Flags & 0x04) != 0;
        
        if (HasPosition)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
            Z = reader.ReadSingle();
        }
        
        if (HasHp)
        {
            Hp = reader.ReadInt32();
        }
        
        if (HasLevel)
        {
            Level = reader.ReadInt32();
        }
    }
    
    // 플래그 설정 헬퍼
    public void SetFlags()
    {
        Flags = 0;
        if (HasPosition) Flags |= 0x01;
        if (HasHp) Flags |= 0x02;
        if (HasLevel) Flags |= 0x04;
    }
}

