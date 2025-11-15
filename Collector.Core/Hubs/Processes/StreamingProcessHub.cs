using System.Threading.Channels;
using Collector.Core.Extensions;
using Microsoft.Extensions.Logging;
using Streaming;

namespace Collector.Core.Hubs.Processes;

public sealed class StreamingProcessHub(ILogger<StreamingProcessHub> logger) : IStreamingProcessHub
{
    public void SendProcessTree(ProcessTreeContract processTreeContract)
    {
        ProcessTreeChannel.Writer.TryWrite(processTreeContract);
    }

    public Channel<ProcessTreeContract> ProcessTreeChannel { get; } = Channel.CreateBounded<ProcessTreeContract>(new BoundedChannelOptions(capacity: 65536)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    }, itemDropped: _ => logger.Throttle(nameof(StreamingProcessHub), itself => itself.LogWarning("A process contract has been lost from its channel"), expiration: TimeSpan.FromMinutes(1)));
}