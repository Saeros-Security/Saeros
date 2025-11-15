using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using Collector.Core.EventProviders;
using Collector.Core.Extensions;
using Collector.Services.Abstractions.EventLogs.Pipelines;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.Extensions;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.Filtering;
using Collector.Services.Implementation.Agent.EventLogs.Pipelines;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using Shared;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.Polling;

public sealed class PollingEventLogConsumer(ILogger logger, IRuleService ruleService, IEventProviderServiceReader eventProviderServiceReader, Action<int> onEvent, CancellationToken cancellationToken) : EventLogConsumer(ruleService: ruleService, eventProviderServiceReader)
{
    private readonly IEventLogPipeline<WinEvent> _eventLogPipeline = new WinEventLogPipeline(logger);
    private readonly IRuleService _ruleService = ruleService;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);
    private static readonly Process Process = Process.GetCurrentProcess();
    private static readonly ConcurrentHashSet<long> Ids = new();
    private static readonly ConcurrentHashSet<string> BlacklistedChannels = new();

    public ISet<string> EnumerateChannels()
    {
        var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in EventIdsByProvider.GetFilteredEventIdsByProvider(ProviderType.Polling, _ruleService.DefaultEventIds))
        {
            if (TryGetPollingChannelNames(pair.Key.ProviderGuid, out var channelNames))
            {
                foreach (var channelName in channelNames)
                {
                    channels.Add(channelName);
                }
            }
        }

        return channels;
    }
    
    public override async Task ConsumeAsync()
    {
        try
        {
            var eventIds = _ruleService.GetEventIds();
            var eventIdsByProvider = EventIdsByProvider.GetFilteredEventIdsByProvider(ProviderType.Polling, _ruleService.DefaultEventIds).Where(kvp => eventIds.Overlaps(kvp.Value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var time = Process.StartTime.ToUniversalTime();
            using var timer = new PeriodicTimer(_pollingInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var queryByChannel = EventLogQueryFiltering.BuildQueryByChannel(eventIdsByProvider, time, channelFactory: providerKey => TryGetPollingChannelNames(providerKey.ProviderGuid, out var channelNames) ? channelNames : []);
                await queryByChannel.ProcessAllAsync(ReadEventLog, cancellationToken);
                time = time.Add(_pollingInterval);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
    }

    private void ReadEventLog(KeyValuePair<string, string> kvp)
    {
        try
        {
            if (BlacklistedChannels.Contains(kvp.Key)) return;
            var eventLogQuery = new EventLogQuery(kvp.Key, PathType.LogName, kvp.Value);
            using var eventLogReader = new EventLogReader(eventLogQuery);
            eventLogReader.BatchSize = 64 * 10;
            while (eventLogReader.ReadEvent() is { } eventRecord)
            {
                using (eventRecord)
                {
                    if (eventRecord.RecordId is null) continue;
                    if (!Ids.Add(eventRecord.RecordId.Value)) continue;
                    if (!eventRecord.TryGetWinEvent(out var winEvent)) continue;
                    onEvent(winEvent.EventId);
                    if (!_eventLogPipeline.Push(winEvent))
                    {
                        var providerName = eventRecord.ProviderName;
                        logger.Throttle(providerName, itself => itself.LogError("An event has been lost because pipeline is full for provider {Provider}", providerName), expiration: TimeSpan.FromMinutes(1));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Throttle(kvp.Key, itself => itself.LogWarning("An error has occurred for channel {Channel}: {Message}", kvp.Key, ex.Message), expiration: TimeSpan.FromMinutes(1));
            if (ex is EventLogNotFoundException)
            {
                BlacklistedChannels.Add(kvp.Key);
            }
        }
    }

    public override IEnumerable<IEventLogPipeline<WinEvent>> GetUserPipelines()
    {
        return [_eventLogPipeline];
    }
}