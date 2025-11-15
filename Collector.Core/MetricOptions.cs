using App.Metrics.Counter;
using App.Metrics.Gauge;
using App.Metrics.Histogram;

namespace Collector.Core;

public static class MetricOptions
{
    public static readonly CounterOptions Detections = new()
    {
        Context = "detections",
        Name = "count"
    };
    
    public static readonly HistogramOptions DetectionDurations = new()
    {
        Context = "detections",
        Name = "duration"
    };
    
    public static readonly GaugeOptions Computers = new()
    {
        Context = "computers",
        Name = "count"
    };

    public static readonly HistogramOptions EventThroughput = new()
    {
        Context = "events",
        Name = "throughput"
    };

    public static readonly HistogramOptions CpuUsage = new()
    {
        Context = "metrics",
        Name = "cpu"
    };
    
    public static readonly HistogramOptions MemoryUsage = new()
    {
        Context = "metrics",
        Name = "memory"
    };
}