using System.Threading.Channels;
using Collector.Core.Extensions;
using Microsoft.Extensions.Logging;
using Streaming;

namespace Collector.Core.Hubs.Metrics;

public sealed class StreamingMetricHub(ILogger<StreamingMetricHub> logger) : IStreamingMetricHub
{
    public void SendMetric(MetricContract metricContract)
    {
        MetricChannel.Writer.TryWrite(metricContract);
    }

    public Channel<MetricContract> MetricChannel { get; } = Channel.CreateBounded<MetricContract>(new BoundedChannelOptions(capacity: 100)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    }, itemDropped: _ => logger.Throttle(nameof(StreamingMetricHub), itself => itself.LogWarning("A metric contract has been lost from its channel"), expiration: TimeSpan.FromMinutes(1)));
}