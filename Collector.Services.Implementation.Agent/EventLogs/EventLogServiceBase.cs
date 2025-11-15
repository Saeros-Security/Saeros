using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using Collector.ActiveDirectory.Exceptions;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Databases.Implementation.Extensions;
using Collector.Detection.Aggregations.Aggregators;
using Collector.Detection.Rules;
using Collector.Services.Abstractions.Detections;
using Collector.Services.Abstractions.EventLogs;
using Collector.Services.Abstractions.EventLogs.Pipelines;
using Collector.Services.Abstractions.Processes;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Abstractions.Tracing;
using Collector.Services.Implementation.Agent.EventLogs.Consumers;
using Collector.Services.Implementation.Rules;
using ConcurrentCollections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Shared;
using Shared.Extensions;
using Shared.Helpers;
using Shared.Models.Console.Requests;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Implementation.Agent.EventLogs;

public abstract class EventLogServiceBase : IEventLogService
{
    private readonly ILogger<EventLogServiceBase> _logger;
    private readonly IDetectionService _eventLogIngestor;
    private readonly IProcessTreeService _processTreeService;
    private readonly ITracingService _tracingService;
    private readonly ConcurrentHashSet<string> _enabledRules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ActionBlock<WinEvent> _eventBlock;
    private readonly ActionBlock<WinEvent> _tracerBlock;
    private readonly DataFlowHelper.PeriodicBlock<Tuple<WinEvent, AggregationRule>> _aggregationBlock;
    private readonly IDisposable _compositeSubscription;
    private readonly Channel<WinEvent> _processTreeChannel = Channel.CreateBounded<WinEvent>(capacity: 1024);
    private readonly Channel<WinEvent> _tracerChannel = Channel.CreateBounded<WinEvent>(capacity: 1024);
    private readonly ConcurrentDictionary<int, long> _eventCountByEventId = new();
    private readonly AsyncRetryPolicy _auditPoliciesRetryPolicy;
    
    private static readonly HashSet<ushort> ProcessTreeEventIds = [4688];
    private static readonly HashSet<ushort> TracerEventIds = [4624, 4625, 4688, 4689];
    
    protected readonly Subject<ConsumptionParameters> StartNewConsumptionSubject = new();
    protected readonly IRuleService RuleService;
    
    protected EventLogServiceBase(ILogger<EventLogServiceBase> logger, IRuleService ruleService, IDetectionService eventLogIngestor, IProcessTreeService processTreeService, ITracingService tracingService, IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        RuleService = ruleService;
        _eventLogIngestor = eventLogIngestor;
        _processTreeService = processTreeService;
        _tracingService = tracingService;
        _eventBlock = CreateEventBlock(hostApplicationLifetime.ApplicationStopping);
        _tracerBlock = CreateTracerBlock(hostApplicationLifetime.ApplicationStopping);
        _aggregationBlock = CreateAggregationBlock(hostApplicationLifetime.ApplicationStopping, out var disposableLink);
        _compositeSubscription = new CompositeDisposable(CreateConsumerSubscription(), CreateConsumptionSubscription(), disposableLink);
        _auditPoliciesRetryPolicy = Policy.Handle<SmbException>().WaitAndRetryForeverAsync((_, _) => TimeSpan.FromSeconds(30),
            (_, retryAttempt, _, _) =>
            {
                _logger.LogError("Could not set audit policies, retrying... (retry attempt: {RetryCount})", retryAttempt);
            }
        );
    }
    
    protected void OnEvent(int eventId)
    {
        _eventCountByEventId.AddOrUpdate(eventId, addValue: 1, updateValueFactory: (_, current) => 
        {
            current++;
            return current;
        });
    }

    private async Task CountEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await _tracingService.TraceAsync(_eventCountByEventId);
                _eventCountByEventId.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
    }

    private ActionBlock<WinEvent> CreateEventBlock(CancellationToken cancellationToken)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            SingleProducerConstrained = false,
            BoundedCapacity = 1024,
            CancellationToken = cancellationToken
        };
        
        return new ActionBlock<WinEvent>(winEvent => ProcessEventAsync(winEvent, cancellationToken), executionDataflow);
    }
    
    private ActionBlock<WinEvent> CreateTracerBlock(CancellationToken cancellationToken)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            SingleProducerConstrained = false,
            BoundedCapacity = 1024,
            CancellationToken = cancellationToken
        };
        
        return new ActionBlock<WinEvent>(winEvent => ProcessTracerAsync(winEvent, cancellationToken), executionDataflow);
    }

    private DataFlowHelper.PeriodicBlock<Tuple<WinEvent, AggregationRule>> CreateAggregationBlock(CancellationToken cancellationToken, out IDisposable disposableLink)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            SingleProducerConstrained = false,
            BoundedCapacity = 12,
            CancellationToken = cancellationToken
        };
        
        var periodicBlock = DataFlowHelper.CreatePeriodicBlock<Tuple<WinEvent, AggregationRule>>(TimeSpan.FromSeconds(5), count: 1000);
        var options = new DataflowLinkOptions { PropagateCompletion = true };
        var propagationBlock = new ActionBlock<IList<Tuple<WinEvent, AggregationRule>>>(async items => await ProcessAggregationsAsync(items, cancellationToken), executionDataflow);
        disposableLink = periodicBlock.LinkTo(propagationBlock, options);
        return periodicBlock;
    }
    
    private async Task ConsumeEventsAsync(IEventLogPipeline<WinEvent> pipeline, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var winEvent in pipeline.Consume(cancellationToken))
            {
                if (ProcessTreeEventIds.Contains(winEvent.EventId) && !_processTreeChannel.Writer.TryWrite(winEvent))
                {
                    _logger.Throttle(winEvent.ProviderName, itself => itself.LogWarning("An event has been lost from the process tree channel"), expiration: TimeSpan.FromMinutes(1));
                }
                
                if (TracerEventIds.Contains(winEvent.EventId) && !_tracerChannel.Writer.TryWrite(winEvent))
                {
                    _logger.Throttle(winEvent.ProviderName, itself => itself.LogWarning("An event has been lost from the tracer channel"), expiration: TimeSpan.FromMinutes(1));
                }
                
                await _eventBlock.SendAsync(winEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
    }
    
    private async Task ConsumeKernelDataAsync(IEventLogPipeline<KernelData> pipeline, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var kernelData in pipeline.Consume(cancellationToken))
            {
                await _tracingService.TraceAsync(kernelData);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
    }

    private async Task ConsumeProcessTreesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var winEvent in _processTreeChannel.Reader.ReadAllAsync(cancellationToken))
            {
                _processTreeService.Add(winEvent.GetWorkstationName(), DomainHelper.DomainName, winEvent.GetProviderGuid(), winEvent.GetEventId(), winEvent.EventData);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
    }
    
    private async Task ConsumeTracersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var winEvent in _tracerChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await _tracerBlock.SendAsync(winEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
    }

    private async Task ProcessEventAsync(WinEvent winEvent, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var rule in RuleService.GetRules(winEvent.EventId))
            {
                if (!_enabledRules.Contains(rule.Id)) continue;
                if (rule is AggregationRule aggregationRule && aggregationRule.TryMatch(winEvent))
                {
                    await _aggregationBlock.SendAsync(new Tuple<WinEvent, AggregationRule>(winEvent, aggregationRule), cancellationToken);
                    continue;
                }
                
                if (rule is StandardRule standardRule && standardRule.TryMatch(winEvent, out var match))
                {
                    OnMatch(match);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }

    private async Task ProcessAggregationsAsync(IList<Tuple<WinEvent, AggregationRule>> tuples, CancellationToken cancellationToken)
    {
        try
        {
            if (tuples.Count == 0) return;
            var winEventsByRule = tuples.GroupBy(tuple => tuple.Item2).ToDictionary(kvp => kvp.Key, kvp => kvp.Select(tuple => tuple.Item1));
            await Aggregator.Instance.TrimExpiredAsync(winEventsByRule, cancellationToken);
            await Aggregator.Instance.AddAsync(winEventsByRule, cancellationToken);
            await winEventsByRule.Keys.ProcessAllAsync(aggregationRule =>
            {
                if (aggregationRule.TryMatch(out var match))
                {
                    OnMatch(match);
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }
    
    private async Task ProcessTracerAsync(WinEvent winEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _tracingService.TraceAsync(winEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }

    private void OnMatch(RuleMatch ruleMatch)
    {
        _eventLogIngestor.Send(ruleMatch);
    }
    
    private IDisposable CreateConsumerSubscription()
    {
        return StartNewConsumptionSubject.Throttle(TimeSpan.FromSeconds(5))
            .Select(parameters =>
            {
                return Observable.FromAsync(ct => ConsumeAsync(parameters, ct));
            })
            .Switch()
            .Subscribe();
    }

    private IDisposable CreateConsumptionSubscription()
    {
        return RuleService.EventLogProviderObservable.Do(parameters => StartNewConsumptionSubject.OnNext(parameters)).Subscribe();
    }
    
    private async Task ConsumeAsync(ConsumptionParameters consumptionParameters, CancellationToken cancellationToken)
    {
        try
        {
            var consumers = await CreateConsumersAsync(consumptionParameters, cancellationToken);
            await Task.WhenAll(
                StartConsumersAsync(consumers, consumptionParameters, cancellationToken),
                ConsumeEventsAsync(consumers, cancellationToken),
                ConsumeProcessTreesAsync(cancellationToken),
                ConsumeTracersAsync(cancellationToken),
                CountEventsAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }

    private async Task StartConsumersAsync(ICollection<EventLogConsumer> consumers, ConsumptionParameters consumptionParameters, CancellationToken cancellationToken)
    {
        try
        {
            if (!consumptionParameters.ProviderChanged)
            {
                while (RuleService.RuleEnablementChannel.TryRead(out var ruleEnablement))
                {
                    EnableRule(ruleEnablement, consumptionParameters);
                }
                
                while (RuleService.RuleDisablementChannel.TryRead(out var ruleDisablement))
                {
                    DisableRule(ruleDisablement, consumptionParameters);
                }
            }
            
            RuleService.RuleCreationChannel.ReadAllAsync(cancellationToken).ToObservable().Select(ruleCreation => Observable.FromAsync(async token => await AddRuleAsync(ruleCreation, token))).Concat().Subscribe(cancellationToken);
            RuleService.RuleEnablementChannel.ReadAllAsync(cancellationToken).ToObservable().Do(ruleEnablement => EnableRule(ruleEnablement, new ConsumptionParameters((RuleHub.AuditPolicyPreference)ruleEnablement.AuditPolicyPreference, ProviderChanged: true))).Subscribe(cancellationToken);
            RuleService.RuleDisablementChannel.ReadAllAsync(cancellationToken).ToObservable().Do(ruleDisablement => DisableRule(ruleDisablement, new ConsumptionParameters((RuleHub.AuditPolicyPreference)ruleDisablement.AuditPolicyPreference, ProviderChanged: true))).Subscribe(cancellationToken);
            await _auditPoliciesRetryPolicy.ExecuteAsync(ct => RuleService.SetAuditPoliciesAsync(consumptionParameters.AuditPolicyPreference, ct), cancellationToken);
            await Task.WhenAll(consumers.Select(consumer => consumer.ConsumeAsync()));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }

    private async Task AddRuleAsync(RuleHub.RuleCreation ruleCreation, CancellationToken cancellationToken)
    {
        try
        {
            if (RuleService is not RuleService ruleService) return;
            var createdRule = await ruleService.CreateRuleAsync(new CreateRule(ruleCreation.Request.Content.ToByteArray(), enabled: ruleCreation.Request.Enabled, waitIndefinitely: ruleCreation.Request.WaitIndefinitely, ruleCreation.Request.GroupName), (RuleHub.AuditPolicyPreference)ruleCreation.Request.AuditPolicyPreference, channelForwarding: false, cancellationToken);
            if (string.IsNullOrWhiteSpace(createdRule.Error))
            {
                if (ruleCreation.Request.Enabled)
                {
                    _enabledRules.Add(createdRule.Id);
                }
                else
                {
                    _enabledRules.TryRemove(createdRule.Id);
                }
                
                ruleCreation.Response.TrySetResult(new RuleCreationResponseContract
                {
                    Title = createdRule.Title,
                    Enabled = ruleCreation.Request.Enabled
                });
            }
            else
            {
                ruleCreation.Response.TrySetResult(new RuleCreationResponseContract
                {
                    Title = createdRule.Title,
                    Enabled = ruleCreation.Request.Enabled,
                    Error = createdRule.Error
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
            ruleCreation.Response.TrySetResult(new RuleCreationResponseContract
            {
                Title = string.Empty,
                Enabled = ruleCreation.Request.Enabled,
                Error = ex.Message
            });
        }
    }
    
    private void EnableRule(RuleIdContract ruleIdContract, ConsumptionParameters parameters)
    {
        var enabled = RuleService.TryEnableRuleById(ruleIdContract.RuleId);
        enabled &= _enabledRules.Add(ruleIdContract.RuleId);
        if (!parameters.ProviderChanged) return;
        if (enabled)
        {
            RuleService.EventLogProviderObservable.OnNext(parameters);
        }
    }
    
    private void DisableRule(RuleIdContract ruleIdContract, ConsumptionParameters parameters)
    {
        var disabled = RuleService.TryDisableRuleById(ruleIdContract.RuleId);
        disabled &= _enabledRules.TryRemove(ruleIdContract.RuleId);
        if (!parameters.ProviderChanged) return;
        if (disabled)
        {
            RuleService.EventLogProviderObservable.OnNext(parameters);
        }
    }
    
    private async Task ConsumeEventsAsync(ICollection<EventLogConsumer> consumers, CancellationToken cancellationToken)
    {
        try
        {
            var userTasks = consumers.SelectMany(consumer => consumer.GetUserPipelines()).Select(pipeline => ConsumeEventsAsync(pipeline, cancellationToken));
            var kernelTasks = consumers.SelectMany(consumer => consumer.GetKernelPipelines()).Select(pipeline => ConsumeKernelDataAsync(pipeline, cancellationToken));
            await Task.WhenAll(Task.WhenAll(userTasks), Task.WhenAll(kernelTasks));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }

    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
    protected abstract Task<ICollection<EventLogConsumer>> CreateConsumersAsync(ConsumptionParameters consumptionParameters, CancellationToken cancellationToken);
    
    public async ValueTask DisposeAsync()
    {
        _eventBlock.Complete();
        _tracerBlock.Complete();
        _compositeSubscription.Dispose();
        StartNewConsumptionSubject.Dispose();
        await _aggregationBlock.DisposeAsync();
    }
}