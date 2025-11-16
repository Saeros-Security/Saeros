using Collector.Services.Abstractions.EventLogs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Agent.HostedServices.EventLogs;

public sealed class EventLogHostedService(ILogger<EventLogHostedService> logger, IEventLogService eventLogService)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            await eventLogService.ExecuteAsync(stoppingToken);
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
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await eventLogService.DisposeAsync();
    }
}