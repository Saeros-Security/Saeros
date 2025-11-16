using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Collector.Core.EventProviders;
using Collector.Core.Extensions;
using Collector.Core.Services;
using Collector.Services.Abstractions.EventLogs.Pipelines;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Abstractions.EventProviders.Registries;
using Collector.Services.Abstractions.Processes;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Abstractions.Tracing;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Extensions;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Helpers;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Kernel;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;
using Collector.Services.Implementation.Agent.EventLogs.Extensions;
using Collector.Services.Implementation.Agent.EventLogs.Pipelines;
using Collector.Services.Implementation.Agent.EventLogs.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.O365.Security.ETW;
using Shared;
using Exception = System.Exception;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW;

// ReSharper disable once InconsistentNaming
public sealed class ETWEventLogConsumer(ILogger logger, IRuleService ruleService, IEventProviderServiceReader eventProviderServiceReader, IGeolocationService geolocationService, IProcessLifecycleObserver processLifecycleObserver, IPeService peService, Action<int> onEvent, CancellationToken token)
    : EventLogConsumer(ruleService, eventProviderServiceReader)
{
    private readonly IRuleService _ruleService = ruleService;
    private readonly IDictionary<string, EventLogSession> _sessions = ManifestEventLogSessions.BuildSessions(logger);
    private readonly ConcurrentDictionary<string, uint> _eventLossByTraceName = new(StringComparer.Ordinal);
    private readonly IEventLogPipeline<KernelData> _kernelPipeline = new KernelDataPipeline(logger);

    private async Task QueryStatsPeriodicallyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    foreach (var pair in _sessions)
                    {
                        if (pair.Value.TryQueryStats(out var stats))
                        {
                            logger.LogInformation("The trace {Name} has handled {Count} events", pair.Value.Name, stats.EventsHandled);
                            var eventLoss = GetEventLossCount(stats, pair.Value.Name);
                            if (eventLoss > 0)
                            {
                                logger.LogWarning("The trace {Name} has lost {Count} events during the last minute", pair.Value.Name, eventLoss);
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogDebug(ex, "An error has occurred");
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
    }

    private uint GetEventLossCount(TraceStats traceStats, string traceName)
    {
        try
        {
            return _eventLossByTraceName.AddOrUpdate(traceName, addValue: traceStats.EventsLost, updateValueFactory: (_, currentValue) => traceStats.EventsLost - currentValue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not query stats for trace {Name}", traceName);
        }

        return 0;
    }

    private bool TryGetManifestedEventLogSession(Guid providerGuid, bool userTrace, [MaybeNullWhen(false)] out EventLogSession session)
    {
        session = null;
        if (!TryGetManifestChannelNames(providerGuid, out var channelNames)) return false;
        foreach (var channelName in channelNames)
        {
            if (_sessions.TryGetValue(channelName, out session))
            {
                return true;
            }
        }

        if (userTrace && _sessions.TryGetValue(EventProviderRegistry.UserSession, out var userSession))
        {
            session = userSession;
            return true;
        }

        return false;
    }

    private void OnCompleted()
    {
        logger.LogDebug("Completed observing events");
    }

    private void OnError(Exception error)
    {
        logger.Throttle(nameof(ETWEventLogConsumer), itself => itself.LogError(error, "An error has occurred"), expiration: TimeSpan.FromMinutes(1));
    }

    private void OnNext(IEventRecordError value)
    {
        logger.Throttle(value.Record.ProviderId.ToString(), itself => itself.LogError("An error has occurred: {Error}", value.Message), expiration: TimeSpan.FromMinutes(1));
    }

    private void OnNext(IEventRecord eventRecord, string? channelName)
    {
        if (token.IsCancellationRequested) return;
        OnNextCore(eventRecord, channelName);
    }

    private bool TryGetChannelName(EventRecord record, string? channelName, out string channel)
    {
        channel = channelName ?? string.Empty;
        if (TryGetManifestedChannelName(record.ProviderId, record.Id, out var channelMatch))
        {
            channel = channelMatch;
        }

        if (string.IsNullOrEmpty(channel)) return false;
        return true;
    }
    
    private void OnNextCore(IEventRecord eventRecord, string? channelName)
    {
        var channel = string.Empty;
        try
        {
            if (eventRecord is not EventRecord record) return;
            if (!TryGetChannelName(record, channelName, out channel)) return;
            var winEvent = record.BuildWinEvent(channel, propertyCount: 8, out var data);
            record.FillProperties(logger, data, IsUnknownPropertyLength);
            if (data.Count == 0) return;
            Push(channel, winEvent);
        }
        catch (Exception ex)
        {
            logger.Throttle(channel, itself => itself.LogError(ex, "An error has occurred while processing an event"), expiration: TimeSpan.FromMinutes(1));
        }
    }

    private bool IsUnknownPropertyLength(EventRecord record, Property property)
    {
        if (TryGetUnknownLengthProperties(record.ProviderId, record.Id, out var unknownLengthProperties))
        {
            if (unknownLengthProperties.Contains(property.Name))
            {
                return true;
            }
        }

        return false;
    }

    private void Push(string channelName, WinEvent winEvent)
    {
        if (_sessions.TryGetValue(channelName, out var eventLogSession))
        {
            onEvent(winEvent.EventId);
            winEvent.EnrichProcesses(peService);
            if (!eventLogSession.Pipeline.Push(winEvent))
            {
                logger.Throttle(channelName, itself => itself.LogError("An event has been lost because pipeline is full for channel {Channel}", channelName), expiration: TimeSpan.FromMinutes(1));
            }
        }
    }

    public override IEnumerable<IEventLogPipeline<WinEvent>> GetUserPipelines()
    {
        foreach (var pipeline in _sessions.Select(session => session.Value.Pipeline))
        {
            yield return pipeline;
        }
    }
    
    public override IEnumerable<IEventLogPipeline<KernelData>> GetKernelPipelines()
    {
        yield return _kernelPipeline;
    }

    public ISet<string> EnumerateChannels()
    {
        var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in EventIdsByProvider.GetFilteredEventIdsByProvider(ProviderType.Manifest, _ruleService.DefaultEventIds))
        {
            foreach (var eventId in pair.Value)
            {
                if (TryGetManifestedChannelName(pair.Key.ProviderGuid, eventId, out var channel))
                {
                    channels.Add(channel);
                }
            }
        }

        return channels;
    }

    public override async Task ConsumeAsync()
    {
        var traceByName = new Dictionary<string, ITrace>(StringComparer.Ordinal);
        var userProviders = new List<Provider>();
        var kernelProviders = new List<KernelProvider>();
        var filters = new List<EventFilter>();
        var subscriptions = new List<IDisposable>();
        var providerCount = 0;
        var eventIds = _ruleService.GetEventIds();
        foreach (var pair in EventIdsByProvider.GetFilteredEventIdsByProvider(ProviderType.Manifest, _ruleService.DefaultEventIds).Where(kvp => eventIds.Overlaps(kvp.Value)))
        {
            if (TryGetManifestedEventLogSession(pair.Key.ProviderGuid, pair.Key.UserTrace, out var session))
            {
                providerCount++;
                if (pair.Value.Count == 0 && TryGetProviderName(pair.Key.ProviderGuid, out var providerName))
                {
                    logger.LogWarning("Provider {Name} ({Guid}) is not filtered. This could cause large volume of events", providerName, pair.Key.ProviderGuid);
                }

                if (session.SessionType == SessionType.User)
                {
                    IUserTrace userTrace = (IUserTrace)session.Trace.Value;
                    var userProvider = new Provider(pair.Key.ProviderGuid);
                    userProvider.Any = Provider.AllBitsSet;
                    if (userTrace is UserTrace trace)
                    {
                        trace.MOFEventProcessingEnabled = false;
                        trace.WPPEventProcessingEnabled = false;
                    }

                    var filter = new EventFilter(pair.Value.Count == 0 ? Filter.AnyEvent() : Filter.Not(Filter.AnyEvent()).Or(pair.Value.Select(value => Filter.EventIdIs(value)).Aggregate((p1, p2) => p1.Or(p2))));
                    userProvider.AddFilter(filter);
                    userTrace.Enable(userProvider);

                    traceByName.TryAdd(session.Name, userTrace);
                    userProviders.Add(userProvider);

                    filters.Add(filter);
                    var eventSubscription = Observable.FromEvent<IEventRecordDelegate, IEventRecord>(h => filter.OnEvent += h, h => filter.OnEvent -= h).Subscribe(onNext: eventRecord => OnNext(eventRecord, session.ChannelName), onError: OnError, onCompleted: OnCompleted);
                    var errorSubscription = Observable.FromEvent<EventRecordErrorDelegate, IEventRecordError>(h => filter.OnError += h, h => filter.OnError -= h).Subscribe(onNext: OnNext, onError: OnError, onCompleted: OnCompleted);
                    subscriptions.Add(new CompositeDisposable(eventSubscription, errorSubscription));
                }
            }
        }

        // TODO: Use https://nasbench.medium.com/finding-detection-and-forensic-goodness-in-etw-providers-7c7a2b5b5f4f to reconstruct SYSMON events
        if (!traceByName.ContainsKey(EventProviderRegistry.KernelTrace) && _sessions.TryGetValue(EventProviderRegistry.KernelSession, out var kernelSession))
        {
            IKernelTrace kernelTrace = (IKernelTrace)kernelSession.Trace.Value;
            traceByName.TryAdd(kernelSession.Name, kernelTrace);
            kernelProviders.Add(ConfigureKernelProvider(kernelTrace, new Microsoft.O365.Security.ETW.Kernel.NetworkTcpipProvider(), subscriptions, new TcpIpKernelConsumer(logger, geolocationService, processLifecycleObserver, _kernelPipeline)));
            kernelProviders.Add(ConfigureKernelProvider(kernelTrace, new Microsoft.O365.Security.ETW.Kernel.ProcessProvider(), subscriptions, new ProcessKernelConsumer(logger)));
        }

        token.ThrowIfCancellationRequested();
        await using var registration = token.Register(() =>
        {
            foreach (var pair in traceByName)
            {
                try
                {
                    if (pair.Key.Equals(EventProviderRegistry.UserTrace) ||
                        pair.Key.Equals(EventProviderRegistry.KernelTrace))
                    {
                        pair.Value.Stop();
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    logger.LogDebug("The service is missing permissions to manage ETW sessions");
                }
                catch (SEHException ex)
                {
                    logger.LogWarning("An error has occurred: {Message}", ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error has occurred");
                }
            }
        });

        // ReSharper disable once MethodSupportsCancellation
        var traceTasks = traceByName.Select(trace => Task.Run(() =>
        {
            try
            {
                trace.Value.Start();
            }
            catch (UnauthorizedAccessException)
            {
                logger.LogDebug("The service is missing permissions to manage ETW sessions");
            }
            catch (NoTraceSessionsRemaining)
            {
                logger.LogError("There are too many active ETW sessions");
            }
            catch (SEHException ex)
            {
                logger.LogWarning("An error has occurred: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error has occurred");
            }
        }));

        try
        {
            logger.LogInformation("Start consuming events from {Count} providers...", providerCount);
            await Task.WhenAll(Task.WhenAll(traceTasks), QueryStatsPeriodicallyAsync(token));
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        foreach (var trace in traceByName.Values)
        {
            try
            {
                if (trace is UserTrace userTrace)
                {
                    userTrace.Dispose();
                }
                else if (trace is KernelTrace kernelTrace)
                {
                    kernelTrace.Dispose();
                }
            }
            catch (UnauthorizedAccessException)
            {
                logger.LogDebug("The service is missing permissions to manage ETW sessions");
            }
            catch (SEHException ex)
            {
                logger.LogWarning("An error has occurred: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error has occurred");
            }
        }

        foreach (var userProvider in userProviders)
        {
            userProvider.Dispose();
        }
        
        foreach (var kernelProvider in kernelProviders)
        {
            kernelProvider.Dispose();
        }

        foreach (var filter in filters)
        {
            filter.Dispose();
        }
    }
    
    private KernelProvider ConfigureKernelProvider(IKernelTrace trace, KernelProvider kernelProvider, IList<IDisposable> subscriptions, AbstractKernelConsumer consumer)
    {
        trace.Enable(kernelProvider);
        var eventSubscription = Observable.FromEvent<IEventRecordDelegate, IEventRecord>(h => kernelProvider.OnEvent += h, h => kernelProvider.OnEvent -= h).Subscribe(consumer);
        var errorSubscription = Observable.FromEvent<EventRecordErrorDelegate, IEventRecordError>(h => kernelProvider.OnError += h, h => kernelProvider.OnError -= h).Subscribe(onNext: OnNext, onError: OnError, onCompleted: OnCompleted);
        subscriptions.Add(new CompositeDisposable(eventSubscription, errorSubscription, consumer));
        return kernelProvider;
    }
}