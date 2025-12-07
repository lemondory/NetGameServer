namespace NetGameServer.Common.Packets;

/// <summary>
/// 로그인 요청 패킷
/// </summary>
public class LoginRequestPacket : PacketBase
{
    public override ushort PacketId => (ushort)PacketType.LoginRequest;
    
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    
    public override byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(PacketId);
        writer.Write(Username);
        writer.Write(Password);
        
        return ms.ToArray();
    }
    
    public override void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var packetId = reader.ReadUInt16();
        Username = reader.ReadString();
        Password = reader.ReadString();
    }
}

