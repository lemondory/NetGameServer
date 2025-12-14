namespace NetGameServer.Common.Packets;

/// <summary>
/// 재연결 요청 패킷
/// </summary>
public class ReconnectRequestPacket : PacketBase
{
    public override ushort PacketId => (ushort)PacketType.ReconnectRequest;
    
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    
    public override byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(PacketId);
        writer.Write(Token);
        writer.Write(Username);
        
        return ms.ToArray();
    }
    
    public override void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var packetId = reader.ReadUInt16();
        Token = reader.ReadString();
        Username = reader.ReadString();
    }
}

