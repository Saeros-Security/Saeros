using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Services.Abstractions.Databases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.HostedServices.Databases;

public sealed class DatabaseHostedService(ILogger<DatabaseHostedService> logger, IDatabaseService databaseService, IServiceProvider serviceProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await UpdatePeriodicallyAsync(stoppingToken);
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
    
    private async Task UpdatePeriodicallyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var logonStore = serviceProvider.GetService<ILogonStore>();
            if (logonStore is not null)
            {
                await logonStore.LoadAsync(cancellationToken);
                using var periodicTimer = new PeriodicTimer(TimeSpan.FromHours(1));
                while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
                {
                    await logonStore.LoadAsync(cancellationToken);
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

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            await databaseService.CreateTablesAsync(cancellationToken);
            await base.StartAsync(cancellationToken);
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