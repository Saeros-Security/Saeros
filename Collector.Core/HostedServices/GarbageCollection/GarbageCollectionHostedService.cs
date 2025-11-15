using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector.Core.HostedServices.GarbageCollection;

public sealed class GarbageCollectionHostedService(ILogger<GarbageCollectionHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                GC.Collect(generation: 2, GCCollectionMode.Forced, blocking: false);
            }
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