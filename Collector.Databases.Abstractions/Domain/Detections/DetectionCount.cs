namespace Collector.Databases.Abstractions.Domain.Detections;

public sealed class DetectionCount(long count, long updated)
{
    public long Count { get; set; } = count;
    public long Updated { get; set; } = updated;
}