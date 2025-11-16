namespace Collector.Services.Abstractions.Dashboards;

public interface IDashboardService
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}