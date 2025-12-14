namespace NetGameServer.Common.Packets;

/// <summary>
/// 패킷 생성 팩토리
/// </summary>
public static class PacketFactory
{
    /// <summary>
    /// 패킷 타입에 따라 패킷 인스턴스 생성
    /// </summary>
    public static PacketBase? CreatePacket(PacketType packetType)
    {
        return packetType switch
        {
            PacketType.LoginRequest => new LoginRequestPacket(),
            PacketType.LoginResponse => new LoginResponsePacket(),
            PacketType.ReconnectRequest => new ReconnectRequestPacket(),
            PacketType.ReconnectResponse => new ReconnectResponsePacket(),
            PacketType.MoveRequest => new MoveRequestPacket(),
            PacketType.ObjectSpawn => new ObjectSpawnPacket(),
            PacketType.ObjectDespawn => new ObjectDespawnPacket(),
            PacketType.ObjectUpdate => new ObjectUpdatePacket(),
            PacketType.ObjectSnapshot => new ObjectSnapshotPacket(),
            _ => null
        };
    }
    
    /// <summary>
    /// 바이트 배열에서 패킷 타입을 읽어 패킷 인스턴스 생성
    /// </summary>
    public static PacketBase? DeserializePacket(byte[] data)
    {
        if (data.Length < 2)
            return null;
            
        try
        {
            var packetId = BitConverter.ToUInt16(data, 0);
            var packetType = (PacketType)packetId;
            
            var packet = CreatePacket(packetType);
            if (packet != null)
            {
                packet.Deserialize(data);
                return packet;
            }
            
            // 패킷 타입을 찾을 수 없음
            return null;
        }
        catch (Exception)
        {
            // 역직렬화 실패
            return null;
        }
    }
}

