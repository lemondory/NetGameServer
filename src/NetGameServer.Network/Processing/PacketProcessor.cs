using System.Threading.Channels;
using NetGameServer.Common.Packets;

using NetGameServer.Network.Sessions;

namespace NetGameServer.Network.Processing;

public class PacketContext
{
    public IClientSession Session { get; }
    public PacketBase Packet { get; }
    public DateTime ReceivedTime { get; }
    public int Priority { get; }
    
    public PacketContext(IClientSession session, PacketBase packet, int priority = 0)
    {
        Session = session;
        Packet = packet;
        ReceivedTime = DateTime.UtcNow;
        Priority = priority;
    }
}

// 패킷 처리 워커. 네트워크 스레드와 분리되어 실행됩니다.
public class PacketProcessor : IDisposable
{
    private readonly PriorityQueue<PacketContext, int> _priorityQueue = new();
    private readonly object _queueLock = new();
    private readonly SemaphoreSlim _queueSemaphore = new(0);
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<Task> _workerTasks;
    public int WorkerCount { get; }
    public Action<PacketContext> PacketHandler { get; set; }
    
    public PacketProcessor(int workerCount, Action<PacketContext>? packetHandler = null)
    {
        WorkerCount = workerCount;
        PacketHandler = packetHandler ?? ((context) => { });
        _cancellationTokenSource = new CancellationTokenSource();
        
        _workerTasks = new List<Task>();
        for (int i = 0; i < WorkerCount; i++)
        {
            var workerId = i;
            _workerTasks.Add(Task.Run(() => ProcessPacketsAsync(workerId, _cancellationTokenSource.Token)));
        }
    }
    
    // 패킷을 우선순위 큐에 추가. 네트워크 스레드에서 호출됩니다.
    public void EnqueuePacket(IClientSession session, PacketBase packet, int priority = 0)
    {
        lock (_queueLock)
        {
            _priorityQueue.Enqueue(new PacketContext(session, packet, priority), -priority);
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
                    try
                    {
                        PacketHandler(context);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"워커 {workerId} 패킷 처리 오류: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"워커 {workerId} 오류: {ex.Message}");
        }
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

