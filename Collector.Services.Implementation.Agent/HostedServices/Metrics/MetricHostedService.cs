using Collector.Services.Abstractions.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Agent.HostedServices.Metrics;

public sealed class MetricHostedService(ILogger<MetricHostedService> logger, IMetricService metricService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            await metricService.ExecuteAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
    }
}