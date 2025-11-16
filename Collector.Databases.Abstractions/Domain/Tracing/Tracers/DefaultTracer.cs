using Streaming;

namespace Collector.Databases.Abstractions.Domain.Tracing.Tracers;

public class DefaultTracer : Tracer
{
    public static readonly DefaultTracer Instance = new();
    
    public override TraceContract ToContract()
    {
        return new TraceContract();
    }
}