using Collector.Services.Abstractions.Domains;
using Collector.Services.Implementation.Bridge.NamedPipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Helpers;

namespace Collector.Services.Implementation.Bridge.HostedServices.Domains;

public sealed class DomainHostedService(ILogger<DomainHostedService> logger, IDomainService domainService, INamedPipeBridge namedPipeBridge) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            if (DomainHelper.DomainJoined)
            {
                domainService.Observe();
            }
            else
            {
                await namedPipeBridge.ExecuteAsync(domain: Shared.Constants.Workgroup, server: MachineNameHelper.FullyQualifiedName, stoppingToken);
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