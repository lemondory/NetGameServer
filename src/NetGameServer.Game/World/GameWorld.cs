using System.Collections.Concurrent;

namespace NetGameServer.Game.World;

// 게임 월드. 모든 맵을 관리합니다.
public class GameWorld : IDisposable
{
    private readonly ConcurrentDictionary<uint, GameMap> _maps = new();
    
    public void AddMap(GameMap map)
    {
        if (_maps.TryAdd(map.MapId, map))
        {
            map.Start();
        }
    }
    
    public GameMap? GetMap(uint mapId)
    {
        _maps.TryGetValue(mapId, out var map);
        return map;
    }
    
    public IEnumerable<GameMap> GetAllMaps()
    {
        return _maps.Values;
    }
    
    public void Dispose()
    {
        foreach (var map in _maps.Values)
        {
            map.Dispose();
        }
        _maps.Clear();
    }
}

