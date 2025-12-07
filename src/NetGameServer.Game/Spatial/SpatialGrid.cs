using System.Collections.Concurrent;

namespace NetGameServer.Game.Spatial;

// Grid 기반 공간 분할 구현 - ECS 호환
public class SpatialGrid : ISpatialPartition
{
    private readonly ConcurrentDictionary<(int x, int z), ConcurrentDictionary<uint, bool>> _cells = new();
    private readonly ConcurrentDictionary<uint, (int cellX, int cellZ)> _entityCells = new();
    private readonly ConcurrentDictionary<uint, (float x, float y, float z)> _positions = new();
    private readonly float _cellSize;
    
    public float CellSize => _cellSize;
    public int EntityCount => _positions.Count;
    
    public SpatialGrid(float cellSize = 10.0f)
    {
        if (cellSize <= 0)
            throw new ArgumentException("Cell size must be greater than 0", nameof(cellSize));
        
        _cellSize = cellSize;
    }
    
    public void Add(uint entityId, float x, float y, float z)
    {
        var cell = GetCell(x, z);
        _positions[entityId] = (x, y, z);
        _entityCells[entityId] = cell;
        
        var cellDict = _cells.GetOrAdd(cell, _ => new ConcurrentDictionary<uint, bool>());
        cellDict[entityId] = true;
    }
    
    public void Update(uint entityId, float x, float y, float z)
    {
        if (!_positions.ContainsKey(entityId))
        {
            Add(entityId, x, y, z);
            return;
        }
        
        var oldCell = _entityCells[entityId];
        var newCell = GetCell(x, z);
        
        if (oldCell != newCell)
        {
            // 셀 이동
            if (_cells.TryGetValue(oldCell, out var oldCellDict))
                oldCellDict.TryRemove(entityId, out _);
            
            var newCellDict = _cells.GetOrAdd(newCell, _ => new ConcurrentDictionary<uint, bool>());
            newCellDict[entityId] = true;
            
            _entityCells[entityId] = newCell;
        }
        
        _positions[entityId] = (x, y, z);
    }
    
    public void Remove(uint entityId)
    {
        if (!_entityCells.TryRemove(entityId, out var cell))
            return;
        
        if (_cells.TryGetValue(cell, out var cellDict))
            cellDict.TryRemove(entityId, out _);
        
        _positions.TryRemove(entityId, out _);
    }
    
    public IEnumerable<uint> QueryRange(float x, float y, float z, float range)
    {
        if (range <= 0)
            return Enumerable.Empty<uint>();
        
        var minCell = GetCell(x - range, z - range);
        var maxCell = GetCell(x + range, z + range);
        var result = new HashSet<uint>();
        
        for (int cx = minCell.x; cx <= maxCell.x; cx++)
        {
            for (int cz = minCell.z; cz <= maxCell.z; cz++)
            {
                if (_cells.TryGetValue((cx, cz), out var cellDict))
                {
                    foreach (var entityId in cellDict.Keys)
                    {
                        if (_positions.TryGetValue(entityId, out var pos))
                        {
                            var distance = GetDistance(x, y, z, pos.x, pos.y, pos.z);
                            if (distance <= range)
                                result.Add(entityId);
                        }
                    }
                }
            }
        }
        
        return result;
    }
    
    public IEnumerable<uint> QueryCell(int cellX, int cellZ)
    {
        if (_cells.TryGetValue((cellX, cellZ), out var cellDict))
            return cellDict.Keys;
        return Enumerable.Empty<uint>();
    }
    
    public (int cellX, int cellZ)? GetCell(uint entityId)
    {
        if (_entityCells.TryGetValue(entityId, out var cell))
            return cell;
        return null;
    }
    
    private (int x, int z) GetCell(float x, float z)
    {
        return ((int)Math.Floor(x / _cellSize), (int)Math.Floor(z / _cellSize));
    }
    
    private static float GetDistance(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var dz = z2 - z1;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}

