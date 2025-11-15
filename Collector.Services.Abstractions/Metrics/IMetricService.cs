namespace Collector.Services.Abstractions.Metrics;

public interface IMetricService
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}