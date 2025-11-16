using System.Threading.Channels;
using Streaming;

namespace Collector.Core.Hubs.Events;

public sealed class StreamingEventHub : IStreamingEventHub
{
    public void Send(EventContract contract)
    {
        EventChannel.Writer.TryWrite(contract);
    }

    public Channel<EventContract> EventChannel { get; } = Channel.CreateBounded<EventContract>(new BoundedChannelOptions(capacity: 1024)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });
}