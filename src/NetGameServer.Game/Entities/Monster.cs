namespace NetGameServer.Game.Entities;

/// <summary>
/// 몬스터
/// </summary>
public class Monster : IGameObject
{
    private static uint _nextId = 10000; // 캐릭터와 구분하기 위해 큰 수에서 시작
    
    public uint ObjectId { get; private set; }
    public GameObjectType ObjectType => GameObjectType.Monster;
    public bool IsActive { get; private set; } = true;
    
    // 위치 정보
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    // AI 상태
    public MonsterState State { get; private set; } = MonsterState.Idle;
    
    // 이동 속도
    public float MoveSpeed { get; set; } = 3.0f;
    
    // 전투 정보
    public int Hp { get; set; } = 50;
    public int MaxHp { get; set; } = 50;
    public int AttackDamage { get; set; } = 10;
    public float AttackRange { get; set; } = 2.0f;
    public float DetectRange { get; set; } = 10.0f;
    
    // AI 변수
    private float _idleTimer = 0;
    private Character? _targetCharacter;
    private float? _patrolTargetX;
    private float? _patrolTargetY;
    private float? _patrolTargetZ;
    
    // 스폰 위치 (리스폰용)
    private float _spawnX;
    private float _spawnY;
    private float _spawnZ;
    
    // AI 업데이트 빈도 제어
    private float _lastUpdateTime = 0;
    private float _lastTargetCheckTime = 0;
    
    // 상태별 업데이트 간격 (초)
    private const float UpdateIntervalIdle = 0.5f;    // Idle: 0.5초마다 (2fps)
    private const float UpdateIntervalPatrol = 0.2f;  // Patrol: 0.2초마다 (5fps)
    private const float UpdateIntervalChase = 0.1f;  // Chase: 0.1초마다 (10fps)
    private const float UpdateIntervalAttack = 0.0f;  // Attack: 매 프레임 (20fps)
    
    // 타겟 감지 빈도
    private const float TargetCheckInterval = 0.3f;   // 0.3초마다 타겟 감지
    
    public Monster()
    {
        // 풀링을 위한 기본 생성자
    }
    
    public Monster(float x = 0, float y = 0, float z = 0)
    {
        ObjectId = _nextId++;
        X = x;
        Y = y;
        Z = z;
        _spawnX = x;
        _spawnY = y;
        _spawnZ = z;
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
        _spawnX = x;
        _spawnY = y;
        _spawnZ = z;
        IsActive = true;
        State = MonsterState.Idle;
        MoveSpeed = 3.0f;
        Hp = 50;
        MaxHp = 50;
        AttackDamage = 10;
        AttackRange = 2.0f;
        DetectRange = 10.0f;
        _idleTimer = 0;
        _targetCharacter = null;
        _patrolTargetX = null;
        _patrolTargetY = null;
        _patrolTargetZ = null;
        _lastUpdateTime = 0;
        _lastTargetCheckTime = 0;
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
        _spawnX = 0;
        _spawnY = 0;
        _spawnZ = 0;
        State = MonsterState.Idle;
        MoveSpeed = 3.0f;
        Hp = 50;
        MaxHp = 50;
        AttackDamage = 10;
        AttackRange = 2.0f;
        DetectRange = 10.0f;
        _idleTimer = 0;
        _targetCharacter = null;
        _patrolTargetX = null;
        _patrolTargetY = null;
        _patrolTargetZ = null;
        _lastUpdateTime = 0;
        _lastTargetCheckTime = 0;
    }
    
    /// <summary>
    /// 업데이트 (업데이트 빈도 제어 포함)
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!IsActive)
            return;
        
        // 상태별 업데이트 간격 체크
        var updateInterval = GetUpdateInterval();
        if (updateInterval > 0)
        {
            _lastUpdateTime += deltaTime;
            if (_lastUpdateTime < updateInterval)
            {
                return; // 아직 업데이트할 시간이 안 됨
            }
            _lastUpdateTime = 0; // 리셋
        }
            
        switch (State)
        {
            case MonsterState.Idle:
                UpdateIdle(deltaTime);
                break;
            case MonsterState.Patrol:
                UpdatePatrol(deltaTime);
                break;
            case MonsterState.Chase:
                UpdateChase(deltaTime);
                break;
            case MonsterState.Attack:
                UpdateAttack(deltaTime);
                break;
        }
    }
    
    /// <summary>
    /// 상태별 업데이트 간격 반환
    /// </summary>
    private float GetUpdateInterval()
    {
        return State switch
        {
            MonsterState.Idle => UpdateIntervalIdle,
            MonsterState.Patrol => UpdateIntervalPatrol,
            MonsterState.Chase => UpdateIntervalChase,
            MonsterState.Attack => UpdateIntervalAttack,
            _ => UpdateIntervalIdle
        };
    }
    
    /// <summary>
    /// 타겟 감지가 필요한지 확인
    /// </summary>
    public bool ShouldCheckTarget(float deltaTime)
    {
        _lastTargetCheckTime += deltaTime;
        if (_lastTargetCheckTime >= TargetCheckInterval)
        {
            _lastTargetCheckTime = 0;
            return true;
        }
        return false;
    }
    
    private void UpdateIdle(float deltaTime)
    {
        _idleTimer += deltaTime;
        if (_idleTimer >= 3.0f) // 3초 후 순찰 시작
        {
            ChangeState(MonsterState.Patrol);
            SetRandomPatrolTarget();
            _idleTimer = 0;
        }
    }
    
    private void UpdatePatrol(float deltaTime)
    {
        if (_patrolTargetX.HasValue)
        {
            MoveTowards(_patrolTargetX.Value, _patrolTargetY!.Value, _patrolTargetZ!.Value, deltaTime);
            
            var distance = GetDistance(_patrolTargetX.Value, _patrolTargetY.Value, _patrolTargetZ.Value);
            if (distance < 0.5f)
            {
                // 목표 도달
                ChangeState(MonsterState.Idle);
                _patrolTargetX = null;
            }
        }
    }
    
    private void UpdateChase(float deltaTime)
    {
        if (_targetCharacter == null || !_targetCharacter.IsActive)
        {
            ChangeState(MonsterState.Idle);
            _targetCharacter = null;
            return;
        }
        
        var distance = GetDistance(_targetCharacter.X, _targetCharacter.Y, _targetCharacter.Z);
        
        if (distance <= AttackRange)
        {
            ChangeState(MonsterState.Attack);
        }
        else if (distance > DetectRange * 1.5f)
        {
            // 너무 멀어지면 추적 중단
            ChangeState(MonsterState.Idle);
            _targetCharacter = null;
        }
        else
        {
            MoveTowards(_targetCharacter.X, _targetCharacter.Y, _targetCharacter.Z, deltaTime);
        }
    }
    
    private void UpdateAttack(float deltaTime)
    {
        if (_targetCharacter == null || !_targetCharacter.IsActive)
        {
            ChangeState(MonsterState.Idle);
            _targetCharacter = null;
            return;
        }
        
        var distance = GetDistance(_targetCharacter.X, _targetCharacter.Y, _targetCharacter.Z);
        if (distance > AttackRange)
        {
            ChangeState(MonsterState.Chase);
        }
        // TODO: 공격 로직 구현
    }
    
    /// <summary>
    /// 타겟 설정 (캐릭터 감지 시)
    /// </summary>
    public void SetTarget(Character character)
    {
        if (State == MonsterState.Idle || State == MonsterState.Patrol)
        {
            _targetCharacter = character;
            State = MonsterState.Chase;
            _lastUpdateTime = 0; // 상태 변경 시 업데이트 타이머 리셋
        }
    }
    
    /// <summary>
    /// 상태 변경 (내부용)
    /// </summary>
    private void ChangeState(MonsterState newState)
    {
        if (State != newState)
        {
            State = newState;
            _lastUpdateTime = 0; // 상태 변경 시 업데이트 타이머 리셋
        }
    }
    
    private void MoveTowards(float targetX, float targetY, float targetZ, float deltaTime)
    {
        var dx = targetX - X;
        var dy = targetY - Y;
        var dz = targetZ - Z;
        
        var distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var moveDistance = MoveSpeed * deltaTime;
        
        if (distance > 0.1f)
        {
            var ratio = Math.Min(moveDistance / distance, 1.0f);
            X += dx * ratio;
            Y += dy * ratio;
            Z += dz * ratio;
        }
    }
    
    private float GetDistance(float x, float y, float z)
    {
        var dx = x - X;
        var dy = y - Y;
        var dz = z - Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    
    private void SetRandomPatrolTarget()
    {
        var random = new Random();
        _patrolTargetX = _spawnX + (random.NextSingle() - 0.5f) * 10;
        _patrolTargetY = _spawnY;
        _patrolTargetZ = _spawnZ + (random.NextSingle() - 0.5f) * 10;
    }
    
    /// <summary>
    /// 데미지 받기
    /// </summary>
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

/// <summary>
/// 몬스터 상태
/// </summary>
public enum MonsterState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Dead
}

