using Shared;
using Streaming;

namespace Collector.Databases.Abstractions.Domain.Tracing.Tracers;

public abstract class Tracer
{
    public abstract TraceContract ToContract();

    protected static string GetProperty(WinEvent winEvent, string propertyName)
    {
        return winEvent.EventData.TryGetValue(propertyName, out var value) ? value : string.Empty;
    }
}
