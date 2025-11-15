using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.Events;

public interface IStreamingEventHub : IEventForwarder
{
    void Send(EventContract eventContract);
}