using Shared;

namespace Collector.Services.Abstractions.Tracing;

public interface ITracingService : IDisposable
{
    ValueTask TraceAsync(WinEvent winEvent, CancellationToken cancellationToken);
    ValueTask TraceAsync(KernelData kernelData);
    ValueTask TraceAsync(IDictionary<int, long> eventCountById);
}