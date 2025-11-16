namespace Collector.Services.Abstractions.EventLogs.Pipelines;

public interface IEventLogPipeline<T>
{
    bool Push(T data);
    
    IAsyncEnumerable<T> Consume(CancellationToken cancellationToken);
}