using Collector.Databases.Abstractions.Stores.Tracing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Bridge.HostedServices.Tracing;

public sealed class TracingHostedService(ILogger<TracingHostedService> logger, ITracingStore tracingStore) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            await tracingStore.ExecuteAsync(stoppingToken);
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