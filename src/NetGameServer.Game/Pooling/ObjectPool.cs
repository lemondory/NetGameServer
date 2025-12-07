using System.Collections.Concurrent;

namespace NetGameServer.Game.Pooling;

/// <summary>
/// 오브젝트 풀 (재사용 가능한 오브젝트 관리)
/// </summary>
public class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentQueue<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _reset;
    private readonly int _maxSize;
    private int _currentSize = 0;
    
    /// <summary>
    /// 오브젝트 풀 생성
    /// </summary>
    /// <param name="factory">오브젝트 생성 함수 (null이면 new T() 사용)</param>
    /// <param name="reset">오브젝트 재사용 전 초기화 함수</param>
    /// <param name="initialSize">초기 풀 크기</param>
    /// <param name="maxSize">최대 풀 크기 (0이면 제한 없음)</param>
    public ObjectPool(Func<T>? factory = null, Action<T>? reset = null, int initialSize = 10, int maxSize = 100)
    {
        _factory = factory ?? (() => new T());
        _reset = reset;
        _maxSize = maxSize;
        
        // 초기 풀 생성
        for (int i = 0; i < initialSize; i++)
        {
            _pool.Enqueue(_factory());
            _currentSize++;
        }
    }
    
    /// <summary>
    /// 풀에서 오브젝트 가져오기
    /// </summary>
    public T Rent()
    {
        if (_pool.TryDequeue(out var obj))
        {
            Interlocked.Decrement(ref _currentSize);
            return obj;
        }
        
        // 풀이 비어있으면 새로 생성
        return _factory();
    }
    
    /// <summary>
    /// 풀에 오브젝트 반환
    /// </summary>
    public void Return(T obj)
    {
        if (obj == null)
            return;
        
        // 초기화
        _reset?.Invoke(obj);
        
        // 최대 크기 체크
        if (_maxSize > 0 && _currentSize >= _maxSize)
        {
            return; // 풀이 가득 차면 버림
        }
        
        _pool.Enqueue(obj);
        Interlocked.Increment(ref _currentSize);
    }
    
    /// <summary>
    /// 현재 풀 크기
    /// </summary>
    public int Count => _currentSize;
    
    /// <summary>
    /// 풀 초기화 (모든 오브젝트 제거)
    /// </summary>
    public void Clear()
    {
        while (_pool.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _currentSize);
        }
    }
}

