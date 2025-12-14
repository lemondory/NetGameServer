namespace NetGameServer.Common.Packets;

public enum PacketType : ushort
{
    LoginRequest = 1000,
    LoginResponse = 1001,
    RegisterRequest = 1002,
    RegisterResponse = 1003,
    ReconnectRequest = 1004,
    ReconnectResponse = 1005,
    
    GameStart = 2000,
    GameAction = 2001,
    GameState = 2002,
    MoveRequest = 2003,      // 이동 요청
    
    // 오브젝트 동기화 패킷
    ObjectSpawn = 3000,      // 오브젝트 스폰
    ObjectDespawn = 3001,    // 오브젝트 제거
    ObjectUpdate = 3002,     // 오브젝트 상태 업데이트 (Delta)
    ObjectSnapshot = 3003,   // 전체 상태 스냅샷
    
    Heartbeat = 9000,
    Error = 9999
}

public static class PacketPriority
{
    // 우선순위가 높을수록 먼저 처리됩니다 (숫자가 클수록 높음)
    public const int Critical = 100;  // 전투, 이동 등 즉시 처리 필요
    public const int High = 50;      // 게임 액션
    public const int Normal = 0;     // 기본
    public const int Low = -50;      // 채팅, UI 업데이트 등
    
    public static int GetPriority(PacketType packetType)
    {
        return packetType switch
        {
            PacketType.GameAction => Critical,
            PacketType.MoveRequest => Critical,   // 이동 요청은 즉시 처리
            PacketType.ObjectUpdate => Critical,  // 오브젝트 업데이트는 즉시 처리
            PacketType.ObjectSpawn => High,
            PacketType.ObjectDespawn => High,
            PacketType.GameState => High,
            PacketType.ObjectSnapshot => Normal,  // 스냅샷은 주기적이므로 Normal
            PacketType.LoginRequest => High,
            PacketType.LoginResponse => High,
            PacketType.ReconnectRequest => High,
            PacketType.ReconnectResponse => High,
            PacketType.Heartbeat => Low,
            _ => Normal
        };
    }
}

