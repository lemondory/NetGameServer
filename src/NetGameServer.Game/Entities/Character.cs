namespace NetGameServer.Game.Entities;

public class Character : IGameObject
{
    private static uint _nextId = 1;
    
    public uint ObjectId { get; private set; }
    public GameObjectType ObjectType => GameObjectType.Character;
    public bool IsActive { get; private set; } = true;
    
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    public float MoveSpeed { get; set; } = 5.0f;
    
    public int Hp { get; set; } = 100;
    public int MaxHp { get; set; } = 100;
    public int Level { get; set; } = 1;
    
    private float? _targetX;
    private float? _targetY;
    private float? _targetZ;
    
    public string? SessionId { get; set; }
    
    public Character()
    {
        // 풀링을 위한 기본 생성자
    }
    
    public Character(float x = 0, float y = 0, float z = 0)
    {
        ObjectId = _nextId++;
        X = x;
        Y = y;
        Z = z;
    }
    
    /// <summary>
    /// 풀에서 가져올 때 초기화
    /// </summary>
    public void Initialize(uint objectId, float x = 0, float y = 0, float z = 0)
    {
        ObjectId = objectId;
        X = x;
        Y = y;
        Z = z;
        IsActive = true;
        MoveSpeed = 5.0f;
        Hp = 100;
        MaxHp = 100;
        Level = 1;
        _targetX = null;
        _targetY = null;
        _targetZ = null;
        SessionId = null;
    }
    
    /// <summary>
    /// 풀에 반환하기 전 초기화
    /// </summary>
    public void Reset()
    {
        IsActive = false;
        ObjectId = 0;
        X = 0;
        Y = 0;
        Z = 0;
        MoveSpeed = 5.0f;
        Hp = 100;
        MaxHp = 100;
        Level = 1;
        _targetX = null;
        _targetY = null;
        _targetZ = null;
        SessionId = null;
    }
    
    public void SetMoveTarget(float x, float y, float z)
    {
        _targetX = x;
        _targetY = y;
        _targetZ = z;
    }
    
    public void Update(float deltaTime)
    {
        if (!IsActive)
            return;
            
        if (_targetX.HasValue)
        {
            MoveTowardsTarget(deltaTime);
        }
    }
    
    private void MoveTowardsTarget(float deltaTime)
    {
        if (!_targetX.HasValue)
            return;
            
        var dx = _targetX.Value - X;
        var dy = _targetY!.Value - Y;
        var dz = _targetZ!.Value - Z;
        
        var distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var moveDistance = MoveSpeed * deltaTime;
        
        if (distance <= moveDistance)
        {
            X = _targetX.Value;
            Y = _targetY.Value;
            Z = _targetZ.Value;
            _targetX = null;
            _targetY = null;
            _targetZ = null;
        }
        else
        {
            var ratio = moveDistance / distance;
            X += dx * ratio;
            Y += dy * ratio;
            Z += dz * ratio;
        }
    }
    
    public void TakeDamage(int damage)
    {
        Hp = Math.Max(0, Hp - damage);
        if (Hp <= 0)
        {
            IsActive = false;
        }
    }
    
    public void Deactivate()
    {
        IsActive = false;
    }
}

