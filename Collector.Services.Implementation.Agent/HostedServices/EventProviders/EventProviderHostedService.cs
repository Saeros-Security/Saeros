using Collector.Core.Extensions;
using Collector.Services.Abstractions.EventProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Collector.Services.Implementation.Agent.HostedServices.EventProviders;

public sealed class EventProviderHostedService(ILogger<EventProviderHostedService> logger, IEventProviderServiceWriter eventProviderService) : IHostedService
{
    private readonly RetryPolicy _policy = Policy.Handle<Exception>().WaitAndRetryForever(_ => TimeSpan.FromSeconds(1), onRetry: (ex, _) =>
    {
        logger.Throttle(nameof(EventProviderHostedService), log => log.LogWarning(ex, "Could not load providers, retrying..."), TimeSpan.FromMinutes(1));
    });
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting...");
            _policy.Execute(eventProviderService.LoadProviders, cancellationToken);
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