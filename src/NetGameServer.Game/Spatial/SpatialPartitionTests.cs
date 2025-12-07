namespace NetGameServer.Game.Spatial;

// 공간 분할 간단한 테스트 (개발/디버깅용)
public static class SpatialPartitionTests
{
    public static void RunBasicTests()
    {
        Console.WriteLine("=== 공간 분할 기본 테스트 ===");
        
        var grid = new SpatialGrid(cellSize: 10.0f);
        
        // 테스트 1: Entity 추가
        grid.Add(1, 5.0f, 0.0f, 5.0f);
        grid.Add(2, 15.0f, 0.0f, 15.0f);
        grid.Add(3, 25.0f, 0.0f, 25.0f);
        Console.WriteLine($"✓ Entity 추가 완료 (3개)");
        
        // 테스트 2: 범위 조회
        var results = grid.QueryRange(5.0f, 0.0f, 5.0f, 5.0f).ToList();
        Console.WriteLine($"✓ 범위 조회 (중심: 5,0,5, 범위: 5): {results.Count}개 발견");
        Console.WriteLine($"  - 발견된 Entity: {string.Join(", ", results)}");
        
        // 테스트 3: 위치 업데이트
        grid.Update(1, 12.0f, 0.0f, 12.0f);
        Console.WriteLine($"✓ Entity 1 위치 업데이트: (5,0,5) → (12,0,12)");
        
        // 테스트 4: 셀 이동 확인
        var cellBefore = grid.GetCell(1);
        Console.WriteLine($"✓ Entity 1 현재 셀: {cellBefore}");
        
        // 테스트 5: 범위 조회 (업데이트 후)
        var results2 = grid.QueryRange(12.0f, 0.0f, 12.0f, 5.0f).ToList();
        Console.WriteLine($"✓ 범위 조회 (중심: 12,0,12, 범위: 5): {results2.Count}개 발견");
        Console.WriteLine($"  - 발견된 Entity: {string.Join(", ", results2)}");
        
        // 테스트 6: Entity 제거
        grid.Remove(1);
        var results3 = grid.QueryRange(12.0f, 0.0f, 12.0f, 5.0f).ToList();
        Console.WriteLine($"✓ Entity 1 제거 후 범위 조회: {results3.Count}개 발견");
        
        Console.WriteLine($"=== 테스트 완료 (Entity 수: {grid.EntityCount}) ===\n");
    }
    
    public static void RunPerformanceTest(int entityCount = 1000)
    {
        Console.WriteLine($"=== 공간 분할 성능 테스트 ({entityCount}개 Entity) ===");
        
        var grid = new SpatialGrid(cellSize: 10.0f);
        var random = new Random(42);
        
        // Entity 추가
        var startTime = DateTime.UtcNow;
        for (uint i = 1; i <= entityCount; i++)
        {
            var x = random.NextSingle() * 100.0f;
            var z = random.NextSingle() * 100.0f;
            grid.Add(i, x, 0.0f, z);
        }
        var addTime = DateTime.UtcNow - startTime;
        Console.WriteLine($"✓ {entityCount}개 Entity 추가: {addTime.TotalMilliseconds:F2}ms");
        
        // 범위 조회 테스트
        var queryCount = 100;
        startTime = DateTime.UtcNow;
        for (int i = 0; i < queryCount; i++)
        {
            var centerX = random.NextSingle() * 100.0f;
            var centerZ = random.NextSingle() * 100.0f;
            var range = 10.0f;
            var results = grid.QueryRange(centerX, 0.0f, centerZ, range).ToList();
        }
        var queryTime = DateTime.UtcNow - startTime;
        Console.WriteLine($"✓ {queryCount}회 범위 조회: {queryTime.TotalMilliseconds:F2}ms (평균: {queryTime.TotalMilliseconds / queryCount:F2}ms/회)");
        
        // 위치 업데이트 테스트
        startTime = DateTime.UtcNow;
        for (uint i = 1; i <= entityCount; i++)
        {
            var x = random.NextSingle() * 100.0f;
            var z = random.NextSingle() * 100.0f;
            grid.Update(i, x, 0.0f, z);
        }
        var updateTime = DateTime.UtcNow - startTime;
        Console.WriteLine($"✓ {entityCount}개 Entity 위치 업데이트: {updateTime.TotalMilliseconds:F2}ms");
        
        Console.WriteLine($"=== 성능 테스트 완료 ===\n");
    }
}

