using Collector.Detection.Aggregations.Aggregators;
using Collector.Detection.Aggregations.Interfaces;
using Collector.Detection.Aggregations.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Agent.HostedServices.Aggregations;

public sealed class AggregationHostedService(ILogger<AggregationHostedService> logger, IAggregationRepository aggregationRepository, IProvideRuleProperties provideRuleProperties) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            Aggregator.Instance = new Aggregator(aggregationRepository, provideRuleProperties, maxEventsPerRule: 1024);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Stopping...");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }

        return Task.CompletedTask;
    }
}