namespace NetGameServer.Game.Spatial;

// 공간 분할 인터페이스 - ECS 호환 설계
// Entity ID 중심으로 설계하여 나중에 ECS 전환 시 재사용 가능
public interface ISpatialPartition
{
    // Entity 추가
    void Add(uint entityId, float x, float y, float z);
    
    // Entity 제거
    void Remove(uint entityId);
    
    // Entity 위치 업데이트
    void Update(uint entityId, float x, float y, float z);
    
    // 범위 내 Entity 조회
    IEnumerable<uint> QueryRange(float x, float y, float z, float range);
    
    // 특정 셀의 Entity 조회
    IEnumerable<uint> QueryCell(int cellX, int cellZ);
    
    // Entity의 현재 셀 조회
    (int cellX, int cellZ)? GetCell(uint entityId);
    
    // 전체 Entity 수
    int EntityCount { get; }
}

