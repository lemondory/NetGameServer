using NetGameServer.Game.Entities;

namespace NetGameServer.Game.Pooling;

/// <summary>
/// 게임 오브젝트 풀 관리
/// </summary>
public static class GameObjectPools
{
    private static uint _nextCharacterId = 1;
    private static uint _nextMonsterId = 10000;
    
    // Character 풀
    private static readonly ObjectPool<Character> CharacterPool = new(
        factory: () => new Character(),
        reset: (c) => c.Reset(),
        initialSize: 50,
        maxSize: 500
    );
    
    // Monster 풀
    private static readonly ObjectPool<Monster> MonsterPool = new(
        factory: () => new Monster(),
        reset: (m) => m.Reset(),
        initialSize: 100,
        maxSize: 1000
    );
    
    /// <summary>
    /// Character 풀에서 가져오기
    /// </summary>
    public static Character RentCharacter(float x = 0, float y = 0, float z = 0)
    {
        var character = CharacterPool.Rent();
        var objectId = Interlocked.Increment(ref _nextCharacterId);
        character.Initialize(objectId, x, y, z);
        return character;
    }
    
    /// <summary>
    /// Character 풀에 반환
    /// </summary>
    public static void ReturnCharacter(Character character)
    {
        if (character != null)
        {
            CharacterPool.Return(character);
        }
    }
    
    /// <summary>
    /// Monster 풀에서 가져오기
    /// </summary>
    public static Monster RentMonster(float x = 0, float y = 0, float z = 0)
    {
        var monster = MonsterPool.Rent();
        var objectId = Interlocked.Increment(ref _nextMonsterId);
        monster.Initialize(objectId, x, y, z);
        return monster;
    }
    
    /// <summary>
    /// Monster 풀에 반환
    /// </summary>
    public static void ReturnMonster(Monster monster)
    {
        if (monster != null)
        {
            MonsterPool.Return(monster);
        }
    }
    
    /// <summary>
    /// 풀 상태 정보
    /// </summary>
    public static (int CharacterCount, int MonsterCount) GetPoolStatus()
    {
        return (CharacterPool.Count, MonsterPool.Count);
    }
}

