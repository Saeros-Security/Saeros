using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.Tracing;

public interface IStreamingTraceHub : ITracingForwarder
{
    void Send(TraceContract traceContract);
}