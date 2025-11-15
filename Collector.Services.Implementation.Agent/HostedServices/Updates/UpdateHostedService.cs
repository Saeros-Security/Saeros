using Collector.Services.Abstractions.Updates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Helpers;

namespace Collector.Services.Implementation.Agent.HostedServices.Updates;

public sealed class UpdateHostedService(ILogger<UpdateHostedService> logger, IUpdateService updateService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!DomainHelper.DomainJoined) return;
            logger.LogInformation("Starting...");
            await updateService.ExecuteAsync(stoppingToken);
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