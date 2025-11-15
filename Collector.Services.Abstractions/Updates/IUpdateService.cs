namespace Collector.Services.Abstractions.Updates;

public interface IUpdateService
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}