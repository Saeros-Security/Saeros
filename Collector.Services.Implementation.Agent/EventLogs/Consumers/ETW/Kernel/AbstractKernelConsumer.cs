using Collector.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.O365.Security.ETW;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Kernel;

internal abstract class AbstractKernelConsumer(ILogger logger) : IObserver<IEventRecord>, IDisposable
{
    public virtual void OnCompleted()
    {
        logger.LogDebug("Completed observing events");
    }

    public virtual void OnError(Exception error)
    {
        logger.Throttle(nameof(AbstractKernelConsumer), itself => itself.LogError(error, "An error has occurred"), expiration: TimeSpan.FromMinutes(1));
    }

    public abstract void OnNext(IEventRecord value);
    public abstract void Dispose();
}