namespace NetGameServer.Common.Packets;

// 오브젝트 전체 상태 스냅샷 패킷 (주기적 전송)
public class ObjectSnapshotPacket : PacketBase
{
    public override ushort PacketId => (ushort)PacketType.ObjectSnapshot;
    
    public class ObjectData
    {
        public uint ObjectId { get; set; }
        public byte ObjectType { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Level { get; set; }
    }
    
    public List<ObjectData> Objects { get; set; } = new();
    
    public override byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(PacketId);
        writer.Write(Objects.Count);
        
        foreach (var obj in Objects)
        {
            writer.Write(obj.ObjectId);
            writer.Write(obj.ObjectType);
            writer.Write(obj.X);
            writer.Write(obj.Y);
            writer.Write(obj.Z);
            writer.Write(obj.Hp);
            writer.Write(obj.MaxHp);
            writer.Write(obj.Level);
        }
        
        return ms.ToArray();
    }
    
    public override void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var packetId = reader.ReadUInt16();
        var count = reader.ReadInt32();
        
        Objects.Clear();
        for (int i = 0; i < count; i++)
        {
            Objects.Add(new ObjectData
            {
                ObjectId = reader.ReadUInt32(),
                ObjectType = reader.ReadByte(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle(),
                Hp = reader.ReadInt32(),
                MaxHp = reader.ReadInt32(),
                Level = reader.ReadInt32()
            });
        }
    }
}

