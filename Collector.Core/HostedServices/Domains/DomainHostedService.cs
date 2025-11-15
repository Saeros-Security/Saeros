using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Shared.Helpers;

namespace Collector.Core.HostedServices.Domains;

public sealed class DomainHostedService(ILogger<DomainHostedService> logger, IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                if (DomainHelper.DomainChanged())
                {
                    logger.LogInformation("Domain has changed, restarting...");
                    applicationLifetime.StopApplication();
                    await Log.CloseAndFlushAsync();
                    Environment.Exit(-1);
                }
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