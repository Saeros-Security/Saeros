using Collector.Services.Abstractions.DomainControllers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Helpers;

namespace Collector.Services.Implementation.Agent.HostedServices.DomainControllers;

public sealed class DomainControllerHostedService(ILogger<DomainControllerHostedService> logger, IDomainControllerService domainControllerService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!DomainHelper.DomainJoined) return;
            logger.LogInformation("Starting...");
            await domainControllerService.ExecuteAsync(stoppingToken);
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