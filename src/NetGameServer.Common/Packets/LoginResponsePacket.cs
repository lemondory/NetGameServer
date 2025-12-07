namespace NetGameServer.Common.Packets;

/// <summary>
/// 로그인 응답 패킷
/// </summary>
public class LoginResponsePacket : PacketBase
{
    public override ushort PacketId => (ushort)PacketType.LoginResponse;
    
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    
    public override byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(PacketId);
        writer.Write(Success);
        writer.Write(Message);
        writer.Write(Token ?? string.Empty);
        
        return ms.ToArray();
    }
    
    public override void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var packetId = reader.ReadUInt16();
        Success = reader.ReadBoolean();
        Message = reader.ReadString();
        var token = reader.ReadString();
        Token = string.IsNullOrEmpty(token) ? null : token;
    }
}

