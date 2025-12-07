namespace NetGameServer.Common.MapData;

/// <summary>
/// 맵 데이터 (Unity와 서버가 공유)
/// </summary>
public class MapData
{
    public uint MapId { get; set; }
    public string MapName { get; set; } = string.Empty;
    
    // 맵 크기
    public float Width { get; set; }
    public float Height { get; set; }
    public float Depth { get; set; }
    
    // 스폰 포인트 (플레이어, NPC 등)
    public List<SpawnPoint> SpawnPoints { get; set; } = new();
    
    // 몬스터 스폰 정보
    public List<MonsterSpawn> MonsterSpawns { get; set; } = new();
    
    // 정적 오브젝트 (NPC, 상자, 포탈 등)
    public List<StaticObject> StaticObjects { get; set; } = new();
    
    // 장애물/지형 정보 (선택)
    public List<Obstacle> Obstacles { get; set; } = new();
}

/// <summary>
/// 스폰 포인트
/// </summary>
public class SpawnPoint
{
    public string Id { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public SpawnPointType Type { get; set; }
    public string? Tag { get; set; } // 추가 태그 (예: "Town", "Dungeon")
}

/// <summary>
/// 스폰 포인트 타입
/// </summary>
public enum SpawnPointType
{
    Player,     // 플레이어 스폰
    Monster,    // 몬스터 스폰
    NPC,        // NPC 스폰
    Portal,     // 포탈
    Item,       // 아이템 스폰
    Other       // 기타
}

/// <summary>
/// 몬스터 스폰 정보
/// </summary>
public class MonsterSpawn
{
    public string MonsterType { get; set; } = string.Empty; // "Goblin", "Orc" 등
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    // 스폰 설정
    public int Count { get; set; } = 1;              // 스폰 개수
    public float RespawnTime { get; set; } = 30.0f;  // 리스폰 시간 (초)
    public float SpawnRadius { get; set; } = 0.0f;   // 스폰 반경 (0이면 정확한 위치)
    
    // 몬스터 속성
    public int? Level { get; set; }                   // 레벨 (null이면 기본값)
    public int? Hp { get; set; }                      // HP (null이면 기본값)
    public float? MoveSpeed { get; set; }             // 이동 속도
    public float? DetectRange { get; set; }          // 감지 범위
    public float? AttackRange { get; set; }          // 공격 범위
    
    // AI 설정
    public bool CanPatrol { get; set; } = true;      // 순찰 가능 여부
    public float PatrolRadius { get; set; } = 10.0f;  // 순찰 반경
}

/// <summary>
/// 정적 오브젝트 (NPC, 상자, 포탈 등)
/// </summary>
public class StaticObject
{
    public string Id { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty; // "NPC", "Chest", "Portal" 등
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    // 회전 (선택)
    public float? RotationX { get; set; }
    public float? RotationY { get; set; }
    public float? RotationZ { get; set; }
    
    // 스케일 (선택)
    public float? ScaleX { get; set; }
    public float? ScaleY { get; set; }
    public float? ScaleZ { get; set; }
    
    // 추가 데이터 (JSON 문자열로 저장)
    public Dictionary<string, string>? Properties { get; set; }
}

/// <summary>
/// 장애물/지형 정보
/// </summary>
public class Obstacle
{
    public string Id { get; set; } = string.Empty;
    public ObstacleType Type { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    // 크기
    public float Width { get; set; }
    public float Height { get; set; }
    public float Depth { get; set; }
    
    // 회전
    public float? RotationX { get; set; }
    public float? RotationY { get; set; }
    public float? RotationZ { get; set; }
}

/// <summary>
/// 장애물 타입
/// </summary>
public enum ObstacleType
{
    Wall,       // 벽
    Box,        // 박스
    Rock,       // 바위
    Tree,       // 나무
    Building,   // 건물
    Other       // 기타
}

