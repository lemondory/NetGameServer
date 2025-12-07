namespace NetGameServer.Common.Packets;

// 이동 요청 패킷
public class MoveRequestPacket : PacketBase
{
    public override ushort PacketId => (ushort)PacketType.MoveRequest;
    
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float TargetZ { get; set; }
    
    public override byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(PacketId);
        writer.Write(TargetX);
        writer.Write(TargetY);
        writer.Write(TargetZ);
        
        return ms.ToArray();
    }
    
    public override void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var packetId = reader.ReadUInt16();
        TargetX = reader.ReadSingle();
        TargetY = reader.ReadSingle();
        TargetZ = reader.ReadSingle();
    }
}

