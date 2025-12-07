using System.Collections.Concurrent;
using NetGameServer.Game.Entities;

namespace NetGameServer.Game.Spatial;

// 공간 분할 어댑터 - OOP용 래퍼
// 나중에 ECS 전환 시 이 어댑터만 교체하면 됨
public class SpatialPartitionAdapter
{
    private readonly ISpatialPartition _spatialPartition;
    private readonly ConcurrentDictionary<uint, IGameObject> _objects;
    
    public SpatialPartitionAdapter(ISpatialPartition spatialPartition, ConcurrentDictionary<uint, IGameObject> objects)
    {
        _spatialPartition = spatialPartition ?? throw new ArgumentNullException(nameof(spatialPartition));
        _objects = objects ?? throw new ArgumentNullException(nameof(objects));
    }
    
    public void AddObject(IGameObject obj)
    {
        if (obj == null)
            return;
        
        _spatialPartition.Add(obj.ObjectId, obj.X, obj.Y, obj.Z);
    }
    
    public void UpdateObject(IGameObject obj)
    {
        if (obj == null)
            return;
        
        _spatialPartition.Update(obj.ObjectId, obj.X, obj.Y, obj.Z);
    }
    
    public void RemoveObject(uint objectId)
    {
        _spatialPartition.Remove(objectId);
    }
    
    public IEnumerable<IGameObject> GetObjectsInRange(float x, float y, float z, float range)
    {
        var entityIds = _spatialPartition.QueryRange(x, y, z, range);
        foreach (var entityId in entityIds)
        {
            if (_objects.TryGetValue(entityId, out var obj))
                yield return obj;
        }
    }
    
    public IEnumerable<T> GetObjectsInRange<T>(float x, float y, float z, float range) where T : class, IGameObject
    {
        var entityIds = _spatialPartition.QueryRange(x, y, z, range);
        foreach (var entityId in entityIds)
        {
            if (_objects.TryGetValue(entityId, out var obj) && obj is T typedObj)
                yield return typedObj;
        }
    }
}

