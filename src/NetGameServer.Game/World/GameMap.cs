using System.Collections.Concurrent;
using NetGameServer.Game.Entities;
using NetGameServer.Game.Pooling;
using NetGameServer.Game.Spatial;
using NetGameServer.Game.Synchronization;

namespace NetGameServer.Game.World;

// 게임 맵. 오브젝트 관리와 업데이트를 담당합니다.
public class GameMap : IDisposable
{
    public uint MapId { get; }
    public string MapName { get; }
    
    private readonly ConcurrentDictionary<uint, IGameObject> _objects = new();
    private readonly ConcurrentDictionary<string, Character> _charactersBySession = new();
    
    // 공간 분할 (ECS 호환 설계)
    private readonly ISpatialPartition _spatialPartition;
    private readonly SpatialPartitionAdapter _spatialAdapter;
    
    // Interest Management
    public InterestManager InterestManager { get; }
    public ObjectStateTracker StateTracker { get; }
    
    private Task? _updateTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly int _updateRate;
    private float _deltaTime;
    
    public GameMap(uint mapId, string mapName, int updateRate = 20, float cellSize = 10.0f, float viewRange = 50.0f)
    {
        MapId = mapId;
        MapName = mapName;
        _updateRate = updateRate;
        _deltaTime = 1.0f / updateRate;
        
        // 공간 분할 초기화
        _spatialPartition = new SpatialGrid(cellSize);
        _spatialAdapter = new SpatialPartitionAdapter(_spatialPartition, _objects);
        
        // Interest Management 초기화
        InterestManager = new InterestManager(viewRange);
        StateTracker = new ObjectStateTracker();
    }
    
    public void Start()
    {
        if (_updateTask != null)
            return;
            
        _cancellationTokenSource = new CancellationTokenSource();
        _updateTask = Task.Run(UpdateLoopAsync);
    }
    
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _updateTask?.Wait();
        _updateTask = null;
    }
    
    private async Task UpdateLoopAsync()
    {
        var updateInterval = TimeSpan.FromSeconds(_deltaTime);
        var idleCheckInterval = TimeSpan.FromSeconds(1.0); // 유저가 없을 때 체크 간격
        
        while (!_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            // 유저가 없으면 업데이트 스킵
            if (CharacterCount == 0)
            {
                await Task.Delay(idleCheckInterval, _cancellationTokenSource.Token);
                continue;
            }
            
            var startTime = DateTime.UtcNow;
            
            UpdateAllObjects();
            ProcessAI();
            
            var elapsed = DateTime.UtcNow - startTime;
            var sleepTime = updateInterval - elapsed;
            
            if (sleepTime > TimeSpan.Zero)
            {
                await Task.Delay(sleepTime, _cancellationTokenSource.Token);
            }
        }
    }
    
    private void UpdateAllObjects()
    {
        // 유저가 없으면 오브젝트 업데이트도 스킵 (몬스터는 Idle 상태 유지)
        if (CharacterCount == 0)
            return;
            
        var objectsToRemove = new List<uint>();
        
        foreach (var obj in _objects.Values)
        {
            var oldX = obj.X;
            var oldY = obj.Y;
            var oldZ = obj.Z;
            
            obj.Update(_deltaTime);
            
            // 위치가 변경되었으면 공간 분할 업데이트
            if (obj.X != oldX || obj.Y != oldY || obj.Z != oldZ)
            {
                _spatialAdapter.UpdateObject(obj);
            }
            
            if (!obj.IsActive)
            {
                objectsToRemove.Add(obj.ObjectId);
            }
        }
        
        foreach (var id in objectsToRemove)
        {
            RemoveObject(id);
        }
    }
    
    private void ProcessAI()
    {
        // 유저가 없으면 AI 처리 스킵
        if (CharacterCount == 0)
            return;
            
        var monsters = _objects.Values.OfType<Monster>().Where(m => m.IsActive).ToList();
        
        foreach (var monster in monsters)
        {
            // 타겟 감지 빈도 조절 (Idle/Patrol 상태일 때만 체크)
            if (monster.State == MonsterState.Idle || monster.State == MonsterState.Patrol)
            {
                if (!monster.ShouldCheckTarget(_deltaTime))
                {
                    continue; // 아직 타겟 체크할 시간이 안 됨
                }
            }
            
            // 공간 분할을 사용해 범위 내 캐릭터만 조회 (O(n²) → O(n))
            var nearbyCharacters = _spatialAdapter.GetObjectsInRange<Character>(
                monster.X, monster.Y, monster.Z, 
                monster.DetectRange * 1.5f); // 여유 범위
            
            Character? nearestCharacter = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var character in nearbyCharacters)
            {
                if (!character.IsActive)
                    continue;
                
                var distance = GetDistance(
                    monster.X, monster.Y, monster.Z,
                    character.X, character.Y, character.Z);
                    
                if (distance < monster.DetectRange && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCharacter = character;
                }
            }
            
            if (nearestCharacter != null)
            {
                monster.SetTarget(nearestCharacter);
            }
        }
    }
    
    public bool AddObject(IGameObject obj)
    {
        if (_objects.TryAdd(obj.ObjectId, obj))
        {
            // 공간 분할에 추가
            _spatialAdapter.AddObject(obj);
            
            if (obj is Character character && !string.IsNullOrEmpty(character.SessionId))
            {
                _charactersBySession.TryAdd(character.SessionId, character);
            }
            return true;
        }
        return false;
    }
    
    public bool RemoveObject(uint objectId)
    {
        if (_objects.TryRemove(objectId, out var obj))
        {
            // 공간 분할에서 제거
            _spatialAdapter.RemoveObject(objectId);
            
            if (obj is Character character)
            {
                if (!string.IsNullOrEmpty(character.SessionId))
                {
                    _charactersBySession.TryRemove(character.SessionId, out _);
                }
                // 풀에 반환
                GameObjectPools.ReturnCharacter(character);
            }
            else if (obj is Monster monster)
            {
                // 풀에 반환
                GameObjectPools.ReturnMonster(monster);
            }
            
            return true;
        }
        return false;
    }
    
    public T? GetObject<T>(uint objectId) where T : class, IGameObject
    {
        if (_objects.TryGetValue(objectId, out var obj) && obj is T typedObj)
        {
            return typedObj;
        }
        return null;
    }
    
    public Character? GetCharacterBySession(string sessionId)
    {
        _charactersBySession.TryGetValue(sessionId, out var character);
        return character;
    }
    
    public IEnumerable<T> GetObjectsOfType<T>() where T : class, IGameObject
    {
        return _objects.Values.OfType<T>();
    }
    
    public IEnumerable<IGameObject> GetObjectsInRange(float x, float y, float z, float range)
    {
        // 공간 분할을 사용해 최적화된 범위 조회
        return _spatialAdapter.GetObjectsInRange(x, y, z, range);
    }
    
    // Interest Management를 위한 오브젝트 조회
    public IEnumerable<uint> GetObjectsInInterest(string sessionId)
    {
        return InterestManager.GetObjectsInInterest(sessionId, _spatialPartition);
    }
    
    private float GetDistance(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var dz = z2 - z1;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    
    public int ObjectCount => _objects.Count;
    public int CharacterCount => _charactersBySession.Count;
    
    public void Dispose()
    {
        Stop();
        _cancellationTokenSource?.Dispose();
    }
}

