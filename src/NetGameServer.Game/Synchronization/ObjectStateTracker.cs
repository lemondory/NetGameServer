namespace NetGameServer.Game.Synchronization;

// 오브젝트 상태 변경 추적 (Delta Compression용)
public class ObjectStateTracker
{
    private readonly Dictionary<uint, ObjectState> _previousStates = new();
    
    public class ObjectState
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Level { get; set; }
    }
    
    // 오브젝트 상태 저장
    public void SaveState(uint objectId, float x, float y, float z, int hp, int maxHp, int level)
    {
        _previousStates[objectId] = new ObjectState
        {
            X = x,
            Y = y,
            Z = z,
            Hp = hp,
            MaxHp = maxHp,
            Level = level
        };
    }
    
    // 변경된 속성만 추출 (Delta)
    public (bool hasPosition, float x, float y, float z, bool hasHp, int hp, bool hasLevel, int level) 
        GetDelta(uint objectId, float x, float y, float z, int hp, int maxHp, int level)
    {
        if (!_previousStates.TryGetValue(objectId, out var previous))
        {
            // 첫 상태 저장
            SaveState(objectId, x, y, z, hp, maxHp, level);
            return (true, x, y, z, true, hp, true, level);
        }
        
        bool hasPosition = previous.X != x || previous.Y != y || previous.Z != z;
        bool hasHp = previous.Hp != hp;
        bool hasLevel = previous.Level != level;
        
        // 변경된 경우에만 상태 업데이트
        if (hasPosition || hasHp || hasLevel)
        {
            SaveState(objectId, x, y, z, hp, maxHp, level);
        }
        
        return (hasPosition, x, y, z, hasHp, hp, hasLevel, level);
    }
    
    // 오브젝트 상태 제거
    public void RemoveState(uint objectId)
    {
        _previousStates.Remove(objectId);
    }
    
    // 전체 상태 초기화
    public void Clear()
    {
        _previousStates.Clear();
    }
}

