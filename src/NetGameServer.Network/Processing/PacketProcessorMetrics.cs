namespace NetGameServer.Network.Processing;

/// <summary>
/// 패킷 처리 성능 통계
/// </summary>
public class PacketProcessorMetrics
{
    public int QueuedPacketCount { get; init; }
    public long TotalProcessed { get; init; }
    public TimeStats ProcessingTime { get; init; } = new(0, 0, 0, 0, 0);
    public TimeStats QueueWaitTime { get; init; } = new(0, 0, 0, 0, 0);
    public IReadOnlyList<WorkerStat> WorkerStats { get; init; } = Array.Empty<WorkerStat>();

    public record TimeStats(double AvgMs, double MaxMs, double P95Ms, double P99Ms, int SampleCount);
    public record WorkerStat(int WorkerId, long ProcessedCount);
}
