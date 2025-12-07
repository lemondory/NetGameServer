using System.Collections.Concurrent;
using NetGameServer.Game.Spatial;
using NetGameServer.Network.Sessions;

namespace NetGameServer.Game.Synchronization;

// Interest Management - 클라이언트별 관심 영역 관리
public class InterestManager
{
    // 클라이언트별 관심 영역 (시야 범위)
    private readonly ConcurrentDictionary<string, InterestArea> _clientInterests = new();
    
    // 오브젝트별 관심 클라이언트 (역방향 인덱스)
    private readonly ConcurrentDictionary<uint, HashSet<string>> _objectClients = new();
    
    private readonly float _defaultViewRange;
    
    public InterestManager(float defaultViewRange = 50.0f)
    {
        _defaultViewRange = defaultViewRange;
    }
    
    // 클라이언트 관심 영역 설정
    public void SetInterestArea(string sessionId, float x, float y, float z, float? viewRange = null)
    {
        var range = viewRange ?? _defaultViewRange;
        _clientInterests[sessionId] = new InterestArea
        {
            X = x,
            Y = y,
            Z = z,
            ViewRange = range
        };
    }
    
    // 클라이언트 관심 영역 제거
    public void RemoveInterestArea(string sessionId)
    {
        if (_clientInterests.TryRemove(sessionId, out _))
        {
            // 역방향 인덱스에서도 제거
            foreach (var objectClients in _objectClients.Values)
            {
                objectClients.Remove(sessionId);
            }
        }
    }
    
    // 오브젝트가 클라이언트의 관심 영역 내에 있는지 확인
    public bool IsObjectInInterest(string sessionId, uint objectId, float x, float y, float z)
    {
        if (!_clientInterests.TryGetValue(sessionId, out var interest))
            return false;
        
        var distance = GetDistance(interest.X, interest.Y, interest.Z, x, y, z);
        return distance <= interest.ViewRange;
    }
    
    // 오브젝트 추가 시 관심 클라이언트 찾기
    public HashSet<string> GetInterestedClients(uint objectId, float x, float y, float z)
    {
        var clients = new HashSet<string>();
        
        foreach (var (sessionId, interest) in _clientInterests)
        {
            var distance = GetDistance(interest.X, interest.Y, interest.Z, x, y, z);
            if (distance <= interest.ViewRange)
            {
                clients.Add(sessionId);
            }
        }
        
        // 역방향 인덱스 업데이트
        _objectClients[objectId] = clients;
        
        return clients;
    }
    
    // 오브젝트 위치 업데이트 시 관심 클라이언트 재계산
    public HashSet<string> UpdateObjectInterest(uint objectId, float oldX, float oldY, float oldZ, 
        float newX, float newY, float newZ)
    {
        var clients = new HashSet<string>();
        
        foreach (var (sessionId, interest) in _clientInterests)
        {
            var oldDistance = GetDistance(interest.X, interest.Y, interest.Z, oldX, oldY, oldZ);
            var newDistance = GetDistance(interest.X, interest.Y, interest.Z, newX, newY, newZ);
            
            // 기존에 관심 영역 내에 있었거나, 새로 들어온 경우
            if (oldDistance <= interest.ViewRange || newDistance <= interest.ViewRange)
            {
                clients.Add(sessionId);
            }
        }
        
        // 역방향 인덱스 업데이트
        _objectClients[objectId] = clients;
        
        return clients;
    }
    
    // 오브젝트 제거 시 관심 클라이언트 반환
    public HashSet<string>? RemoveObjectInterest(uint objectId)
    {
        if (_objectClients.TryRemove(objectId, out var clients))
        {
            return clients;
        }
        return null;
    }
    
    // 클라이언트의 관심 영역 내 오브젝트 조회
    public IEnumerable<uint> GetObjectsInInterest(string sessionId, ISpatialPartition spatialPartition)
    {
        if (!_clientInterests.TryGetValue(sessionId, out var interest))
            return Enumerable.Empty<uint>();
        
        return spatialPartition.QueryRange(interest.X, interest.Y, interest.Z, interest.ViewRange);
    }
    
    private static float GetDistance(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var dz = z2 - z1;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    
    private class InterestArea
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float ViewRange { get; set; }
    }
}

