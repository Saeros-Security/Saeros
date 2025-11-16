namespace Collector.Services.Abstractions.Databases;

public interface IDatabaseService
{
    Task CreateTablesAsync(CancellationToken cancellationToken);
}