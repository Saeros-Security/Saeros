namespace Collector.Services.Abstractions.EventLogs;

public interface IEventLogService : IAsyncDisposable
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}