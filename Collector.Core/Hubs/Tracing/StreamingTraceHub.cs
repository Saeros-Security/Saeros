using System.Threading.Channels;
using Collector.Core.Extensions;
using Microsoft.Extensions.Logging;
using Streaming;

namespace Collector.Core.Hubs.Tracing;

public sealed class StreamingTraceHub(ILogger<StreamingTraceHub> logger) : IStreamingTraceHub
{
    public void Send(TraceContract traceContract)
    {
        TraceChannel.Writer.TryWrite(traceContract);
    }

    public Channel<TraceContract> TraceChannel { get; } = Channel.CreateBounded<TraceContract>(new BoundedChannelOptions(capacity: 1024)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    }, itemDropped: _ => logger.Throttle(nameof(StreamingTraceHub), itself => itself.LogWarning("A trace contract has been lost from its channel"), expiration: TimeSpan.FromMinutes(1)));
}