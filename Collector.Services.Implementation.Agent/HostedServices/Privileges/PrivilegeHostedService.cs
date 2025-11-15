using Collector.Services.Abstractions.Privileges;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Helpers;

namespace Collector.Services.Implementation.Agent.HostedServices.Privileges;

public sealed class PrivilegeHostedService(ILogger<PrivilegeHostedService> logger, IPrivilegeService privilegeService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (DomainHelper.DomainJoined) return Task.CompletedTask;
            logger.LogInformation("Starting...");
            privilegeService.SetPrivileges();
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