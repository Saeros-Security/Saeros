using System.Threading.Channels;
using Collector.Core.Extensions;
using Collector.Services.Abstractions.EventLogs.Pipelines;
using Microsoft.Extensions.Logging;
using Shared;

namespace Collector.Services.Implementation.Agent.EventLogs.Pipelines;

public sealed class WinEventLogPipeline : IEventLogPipeline<WinEvent>
{
    private readonly Channel<WinEvent> _winEventChannel;

    public WinEventLogPipeline(ILogger logger)
    {
        var options = new BoundedChannelOptions(capacity: 1024 * Environment.ProcessorCount)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };

        _winEventChannel = Channel.CreateBounded<WinEvent>(options, winEventLogDropped => logger.Throttle(winEventLogDropped.ProviderName, itself => itself.LogWarning("EventLog from provider {Provider} was dropped", winEventLogDropped.ProviderName), expiration: TimeSpan.FromMinutes(1)));
    }

    public bool Push(WinEvent winEvent)
    {
        return _winEventChannel.Writer.TryWrite(winEvent);
    }

    public IAsyncEnumerable<WinEvent> Consume(CancellationToken cancellationToken)
    {
        return _winEventChannel.Reader.ReadAllAsync(cancellationToken);
    }
}