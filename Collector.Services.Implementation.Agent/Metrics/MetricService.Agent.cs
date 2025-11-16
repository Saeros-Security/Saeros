using App.Metrics;
using Collector.Core.Hubs.Metrics;
using Microsoft.Extensions.Logging;
using Streaming;

namespace Collector.Services.Implementation.Agent.Metrics;

public sealed class MetricServiceAgent(ILogger<MetricServiceAgent> logger, IMetricsRoot metrics, IStreamingMetricHub streamingMetricHub) : MetricService(logger, metrics)
{
    protected override void Store(MetricContract metricContract)
    {
        streamingMetricHub.SendMetric(metricContract);
    }
}