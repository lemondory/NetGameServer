using System.Threading.Channels;
using NetGameServer.Common.Packets;
using NetGameServer.Common.Packets.Proto;
using NetGameServer.Network.Sessions;
using Serilog;

namespace NetGameServer.Network.Processing;

public class PacketContext
{
    public IClientSession Session { get; }
    public GamePacket Packet { get; }
    public DateTime ReceivedTime { get; }
    public DateTime EnqueuedAt { get; }
    public int Priority { get; }

    public PacketContext(IClientSession session, GamePacket packet, int priority = 0, DateTime? enqueuedAt = null)
    {
        Session = session;
        Packet = packet;
        ReceivedTime = DateTime.UtcNow;
        EnqueuedAt = enqueuedAt ?? DateTime.UtcNow;
        Priority = priority;
    }
}

// 패킷 처리 워커. 네트워크 스레드와 분리되어 실행됩니다.
public class PacketProcessor : IDisposable
{
    private const int MetricsSampleSize = 1000;
    private readonly PriorityQueue<PacketContext, int> _priorityQueue = new();
    private readonly object _queueLock = new();
    private readonly SemaphoreSlim _queueSemaphore = new(0);
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<Task> _workerTasks;
    private readonly long[] _workerProcessedCounts;
    private readonly double[] _processingTimeSamples;
    private readonly double[] _queueWaitTimeSamples;
    private int _processingTimeIndex;
    private int _queueWaitTimeIndex;
    private int _totalProcessed;
    private readonly object _metricsLock = new();

    public int WorkerCount { get; }
    public Action<PacketContext> PacketHandler { get; set; }

    public PacketProcessor(int workerCount, Action<PacketContext>? packetHandler = null)
    {
        WorkerCount = workerCount;
        PacketHandler = packetHandler ?? ((context) => { });
        _cancellationTokenSource = new CancellationTokenSource();
        _workerProcessedCounts = new long[workerCount];
        _processingTimeSamples = new double[MetricsSampleSize];
        _queueWaitTimeSamples = new double[MetricsSampleSize];
        _processingTimeIndex = 0;
        _queueWaitTimeIndex = 0;

        _workerTasks = new List<Task>();
        for (int i = 0; i < WorkerCount; i++)
        {
            var workerId = i;
            _workerTasks.Add(Task.Run(() => ProcessPacketsAsync(workerId, _cancellationTokenSource.Token)));
        }
    }

    // 패킷을 우선순위 큐에 추가. 네트워크 스레드에서 호출됩니다.
    public void EnqueuePacket(IClientSession session, GamePacket packet, int priority = 0)
    {
        var enqueuedAt = DateTime.UtcNow;
        lock (_queueLock)
        {
            _priorityQueue.Enqueue(new PacketContext(session, packet, priority, enqueuedAt), -priority);
            _queueSemaphore.Release();
        }
    }

    private async Task ProcessPacketsAsync(int workerId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _queueSemaphore.WaitAsync(cancellationToken);

                PacketContext? context = null;
                lock (_queueLock)
                {
                    if (_priorityQueue.Count > 0)
                    {
                        _priorityQueue.TryDequeue(out context, out _);
                    }
                }

                if (context != null)
                {
                    var dequeueTime = DateTime.UtcNow;
                    var queueWaitMs = (dequeueTime - context.EnqueuedAt).TotalMilliseconds;

                    try
                    {
                        var start = DateTime.UtcNow;
                        PacketHandler(context);
                        var processingMs = (DateTime.UtcNow - start).TotalMilliseconds;

                        RecordMetrics(workerId, processingMs, queueWaitMs);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "워커 {WorkerId} 패킷 처리 오류: {Message}", workerId, ex.Message);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "워커 {WorkerId} 오류: {Message}", workerId, ex.Message);
        }
    }

    private void RecordMetrics(int workerId, double processingMs, double queueWaitMs)
    {
        Interlocked.Increment(ref _totalProcessed);
        Interlocked.Increment(ref _workerProcessedCounts[workerId]);

        lock (_metricsLock)
        {
            _processingTimeSamples[_processingTimeIndex % MetricsSampleSize] = processingMs;
            _processingTimeIndex++;
            _queueWaitTimeSamples[_queueWaitTimeIndex % MetricsSampleSize] = queueWaitMs;
            _queueWaitTimeIndex++;
        }
    }

    /// <summary>
    /// 성능 통계 조회 (평균, 최대, P95, P99)
    /// </summary>
    public PacketProcessorMetrics GetMetrics()
    {
        int queuedCount;
        int totalProcessed;
        long[] workerCounts;
        double[] processingCopy;
        double[] queueWaitCopy;
        int processingCount;
        int queueWaitCount;

        lock (_queueLock)
        {
            queuedCount = _priorityQueue.Count;
        }

        totalProcessed = Interlocked.CompareExchange(ref _totalProcessed, 0, 0);
        workerCounts = new long[WorkerCount];
        Array.Copy(_workerProcessedCounts, workerCounts, WorkerCount);

        lock (_metricsLock)
        {
            processingCount = Math.Min(_processingTimeIndex, MetricsSampleSize);
            queueWaitCount = Math.Min(_queueWaitTimeIndex, MetricsSampleSize);
            processingCopy = new double[processingCount];
            queueWaitCopy = new double[queueWaitCount];
            Array.Copy(_processingTimeSamples, processingCopy, processingCount);
            Array.Copy(_queueWaitTimeSamples, queueWaitCopy, queueWaitCount);
        }

        return new PacketProcessorMetrics
        {
            QueuedPacketCount = queuedCount,
            TotalProcessed = totalProcessed,
            ProcessingTime = ComputeTimeStats(processingCopy),
            QueueWaitTime = ComputeTimeStats(queueWaitCopy),
            WorkerStats = workerCounts.Select((c, i) => new PacketProcessorMetrics.WorkerStat(i, c)).ToList()
        };
    }

    private static PacketProcessorMetrics.TimeStats ComputeTimeStats(double[] samples)
    {
        if (samples.Length == 0)
            return new PacketProcessorMetrics.TimeStats(0, 0, 0, 0, 0);

        var sorted = (double[])samples.Clone();
        Array.Sort(sorted);
        var n = sorted.Length;
        var avg = sorted.Average();
        var max = sorted[n - 1];
        var p95 = sorted[(int)(n * 0.95)];
        var p99 = sorted[(int)(n * 0.99)];

        return new PacketProcessorMetrics.TimeStats(avg, max, p95, p99, n);
    }

    public int QueuedPacketCount
    {
        get
        {
            lock (_queueLock)
            {
                return _priorityQueue.Count;
            }
        }
    }
    
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _queueSemaphore.Release(WorkerCount);
        Task.WaitAll(_workerTasks.ToArray(), TimeSpan.FromSeconds(5));
        _cancellationTokenSource.Dispose();
        _queueSemaphore.Dispose();
    }
}

