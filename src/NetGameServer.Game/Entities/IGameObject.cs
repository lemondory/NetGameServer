namespace NetGameServer.Game.Entities;

/// <summary>
/// 게임 오브젝트 인터페이스
/// </summary>
public interface IGameObject
{
    /// <summary>
    /// 오브젝트 ID
    /// </summary>
    uint ObjectId { get; }
    
    /// <summary>
    /// 오브젝트 타입
    /// </summary>
    GameObjectType ObjectType { get; }
    
    /// <summary>
    /// 위치 X
    /// </summary>
    float X { get; set; }
    
    /// <summary>
    /// 위치 Y
    /// </summary>
    float Y { get; set; }
    
    /// <summary>
    /// 위치 Z
    /// </summary>
    float Z { get; set; }
    
    /// <summary>
    /// 업데이트 (게임 루프에서 호출)
    /// </summary>
    void Update(float deltaTime);
    
    /// <summary>
    /// 오브젝트가 활성화되어 있는지
    /// </summary>
    bool IsActive { get; }
}

/// <summary>
/// 게임 오브젝트 타입
/// </summary>
public enum GameObjectType
{
    Character,
    Monster,
    Npc,
    Item,
    Projectile,
    Effect
}

