using System.Diagnostics.Eventing.Reader;
using System.Reactive.Linq;
using Collector.Core.EventProviders;
using Collector.Core.Extensions;
using Collector.Services.Abstractions.EventLogs.Pipelines;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.Extensions;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.Filtering;
using Collector.Services.Implementation.Agent.EventLogs.Pipelines;
using Microsoft.Extensions.Logging;
using Shared;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.Mof;

public sealed class MofEventLogConsumer(ILogger logger, IRuleService ruleService, IEventProviderServiceReader eventProviderServiceReader, Action<int> onEvent, CancellationToken cancellationToken) : EventLogConsumer(ruleService, eventProviderServiceReader)
{
    private readonly IRuleService _ruleService = ruleService;
    private readonly IEventLogPipeline<WinEvent> _eventLogPipeline = new WinEventLogPipeline(logger);
    
    private void OnCompleted()
    {
        logger.LogDebug("Completed observing events");
    }

    private void OnError(Exception error)
    {
        if (error is not OperationCanceledException)
        {
            logger.Throttle(nameof(MofEventLogConsumer), itself => itself.LogError(error, "An error has occurred"), expiration: TimeSpan.FromMinutes(1));
        }
    }

    private void OnNext(EventRecord eventRecord, Exception? exception)
    {
        if (exception is not null)
        {
            if (exception is OperationCanceledException) return;
            logger.Throttle(nameof(MofEventLogConsumer), itself => itself.LogError(exception, "An error has occurred"), expiration: TimeSpan.FromMinutes(1));
            return;
        }

        using (eventRecord)
        {
            if (!eventRecord.TryGetWinEvent(out var winEvent)) return;
            onEvent(winEvent.EventId);
            if (!_eventLogPipeline.Push(winEvent))
            {
                logger.Throttle(eventRecord.ProviderName, itself => itself.LogError("An event has been lost because pipeline is full for provider {Provider}", eventRecord.ProviderName), expiration: TimeSpan.FromMinutes(1));
            }
        }
    }

    public override IEnumerable<IEventLogPipeline<WinEvent>> GetUserPipelines()
    {
        return [_eventLogPipeline];
    }

    public ISet<string> EnumerateChannels()
    {
        var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in EventIdsByProvider.GetFilteredEventIdsByProvider(ProviderType.Mof, _ruleService.DefaultEventIds))
        {
            if (TryGetMofChannelName(pair.Key.ProviderGuid, out var channelName))
            {
                channels.Add(channelName);
            }
        }

        return channels;
    }

    public override async Task ConsumeAsync()
    {
        var watchers = new List<EventLogWatcher>();
        var subscriptions = new List<IDisposable>();
        var eventIds = _ruleService.GetEventIds();
        var eventIdsByProvider = EventIdsByProvider.GetFilteredEventIdsByProvider(ProviderType.Mof, _ruleService.DefaultEventIds).Where(kvp => eventIds.Overlaps(kvp.Value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var queryByChannel = EventLogQueryFiltering.BuildQueryByChannel(eventIdsByProvider, providerKey => TryGetMofChannelName(providerKey.ProviderGuid, out var channelName) ? [channelName] : []);
        foreach (var kvp in queryByChannel)
        {
            var watcher = new EventLogWatcher(new EventLogQuery(kvp.Key, PathType.LogName, kvp.Value));
            watcher.Enabled = true;
            var eventSubscription = Observable.FromEventPattern<EventRecordWrittenEventArgs>(h => watcher.EventRecordWritten += h, h => watcher.EventRecordWritten -= h).Subscribe(onNext: args => OnNext(args.EventArgs.EventRecord, args.EventArgs.EventException), onError: OnError, onCompleted: OnCompleted);
            subscriptions.Add(eventSubscription);
            watchers.Add(watcher);
        }

        try
        {
            await Task.Delay(-1, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        foreach (var watcher in watchers)
        {
            watcher.Dispose();
        }
    }
}