using System.Threading.Channels;
using Collector.Core.Extensions;
using Microsoft.Extensions.Logging;
using Shared;
using Streaming;

namespace Collector.Core.Hubs.Detections;

public sealed class StreamingDetectionHub(ILogger<StreamingDetectionHub> logger, CollectorMode collectorMode) : IStreamingDetectionHub
{
    public void SendDetection(DetectionContract detectionContract)
    {
        DetectionChannel.Writer.TryWrite(detectionContract);
    }

    public Channel<DetectionContract> DetectionChannel { get; } = collectorMode == CollectorMode.Agent
        ? Channel.CreateBounded<DetectionContract>(new BoundedChannelOptions(capacity: 100_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        }, itemDropped: _ => logger.Throttle(nameof(StreamingDetectionHub), itself => itself.LogWarning("A detection contract has been lost from its channel"), expiration: TimeSpan.FromMinutes(1)))
        : Channel.CreateBounded<DetectionContract>(new BoundedChannelOptions(capacity: 100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
}