using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.Metrics;

public interface IStreamingMetricHub : IMetricForwarder
{
    void SendMetric(MetricContract metricContract);
}