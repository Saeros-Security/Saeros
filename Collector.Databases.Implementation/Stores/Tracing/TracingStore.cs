using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using App.Metrics;
using Collector.Core;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Core.Hubs.Events;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Abstractions.Stores.Tracing;
using Collector.Databases.Abstractions.Stores.Tracing.Buckets;
using Collector.Databases.Implementation.Caching.Series;
using Collector.Databases.Implementation.Contexts.Tracing;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Extensions;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Kernel;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuikGraph;
using Shared.Extensions;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Streaming;
using User = Collector.Databases.Implementation.Stores.Tracing.Buckets.Users.User;

namespace Collector.Databases.Implementation.Stores.Tracing;

public sealed class TracingStore : ITracingStore
{
    private const string ElevatedTokenType = "%%1937";

    private readonly ILogger<TracingStore> _logger;
    private readonly IMetricsRoot _metrics;
    private readonly UserBucket _userBucket;
    private readonly WorkstationBucket _workstationBucket;
    private readonly ProcessBucket _processBucket;
    private readonly ProcessTreeBucket _processTreeBucket;
    private readonly DataFlowHelper.PeriodicBlock<TraceContract> _processingBlock;
    private readonly ITracingRepository _tracingRepository;
    private readonly TracingContext _tracingContext;
    private readonly IDisposable _subscription;
    private readonly EventSeries _eventSeries;
    private readonly NetworkSeries _networkSeries;
    private readonly LogonSeries _logonSeries;
    private readonly IStreamingEventHub _streamingEventHub;
    
    public TracingStore(ILoggerFactory loggerFactory, IMetricsRoot metrics, IHostApplicationLifetime applicationLifetime, IGeolocationService geolocationService, ITracingRepository tracingRepository, IStreamingEventHub streamingEventHub, TracingContext tracingContext, ILogonStore logonStore, EventSeries eventSeries, NetworkSeries networkSeries)
    {
        _logger = loggerFactory.CreateLogger<TracingStore>();
        _metrics = metrics;
        _tracingRepository = tracingRepository;
        _streamingEventHub = streamingEventHub;
        _tracingContext = tracingContext;
        _userBucket = new UserBucket(tracingRepository, geolocationService, logonStore, GetBucket);
        _workstationBucket = new WorkstationBucket(tracingRepository, geolocationService, logonStore, GetBucket);
        _processBucket = new ProcessBucket(tracingRepository, geolocationService, logonStore, GetBucket);
        _processTreeBucket = new ProcessTreeBucket(tracingRepository, geolocationService, logonStore, GetBucket);
        _processingBlock = CreateProcessingBlock(applicationLifetime.ApplicationStopping, out var disposableLink);
        _eventSeries = eventSeries;
        _networkSeries = networkSeries;
        _logonSeries = new LogonSeries();
        _subscription = disposableLink;
    }
    
    private DataFlowHelper.PeriodicBlock<TraceContract> CreateProcessingBlock(CancellationToken cancellationToken, out IDisposable disposableLink)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            SingleProducerConstrained = false,
            BoundedCapacity = 8,
            CancellationToken = cancellationToken,
            EnsureOrdered = true
        };

        var periodicBlock = DataFlowHelper.CreatePeriodicBlock<TraceContract>(TimeSpan.FromSeconds(1), count: 1000);
        var options = new DataflowLinkOptions { PropagateCompletion = true };
        var deserializationBlock = new TransformBlock<IList<TraceContract>, IEnumerable<Tracer>>(DeserializeTrace, executionDataflow);
        var processingBlock = new ActionBlock<IEnumerable<Tracer>>(async tracers => await ProcessAsync(tracers, cancellationToken), executionDataflow);
        var deserializationLink = periodicBlock.LinkTo(deserializationBlock, options);
        var processingLink = deserializationBlock.LinkTo(processingBlock, options);
        disposableLink = new CompositeDisposable(deserializationLink, processingLink);
        return periodicBlock;
    }

    private IBucket GetBucket(Type type)
    {
        if (type == typeof(UserBucket))
        {
            return _userBucket;
        }
        
        if (type == typeof(WorkstationBucket))
        {
            return _workstationBucket;
        }
        
        if (type == typeof(ProcessBucket))
        {
            return _processBucket;
        }
        
        if (type == typeof(ProcessTreeBucket))
        {
            return _processTreeBucket;
        }

        throw new Exception("Could not find bucket");
    }
    
    private async Task<IEdgeListGraph<TracingNode, IEdge<TracingNode>>> PopulateGraphAsync(TracingQuery query, CancellationToken cancellationToken)
    {
        var builder = new GraphBuilder(_tracingContext, _userBucket, _workstationBucket);
        return await builder.BuildAsync(query, cancellationToken);
    }

    private static bool TryGetSubjectLogonId(Logon4624 _4624, out long subjectLogonId)
    {
        subjectLogonId = LogonHelper.FromLogonId(_4624.SubjectLogonId);
        return subjectLogonId > 0L;
    }
    
    private static bool TryGetTargetLogonId(Logon4624 _4624, out long targetLogonId)
    {
        targetLogonId = LogonHelper.FromLogonId(_4624.TargetLogonId);
        return targetLogonId > 0L;
    }
    
    private static bool TryGetSubjectLogonId(Process4688 _4688, out long subjectLogonId)
    {
        subjectLogonId = LogonHelper.FromLogonId(_4688.SubjectLogonId);
        return subjectLogonId > 0L;
    }
    
    private static bool TryGetTargetLogonId(Process4688 _4688, out long targetLogonId)
    {
        targetLogonId = LogonHelper.FromLogonId(_4688.TargetLogonId);
        return targetLogonId > 0L;
    }

    private static IEnumerable<Tracer> DeserializeTrace(IList<TraceContract> traces)
    {
        return traces.Select(trace => JsonSerializer.Deserialize<Tracer>(trace.Content.Span, TracerJsonExtensions.Options)!);
    }
    
    private async ValueTask ProcessTracerAsync(LogonTracer logonTracer, CancellationToken cancellationToken)
    {
        if (logonTracer is Logon4624 _4624)
        {
            var date = _4624.Date;
            if (!_4624.SubjectUserSid.IsKnownSid() && TryGetSubjectLogonId(_4624, out var subjectLogonId))
            {
                await _workstationBucket.AddAsync(subjectLogonId, new Workstation(_4624.WorkstationName, _4624.WorkstationIpAddress, _4624.Domain, date), cancellationToken);
                if (!_4624.SubjectUserName.EndsWith('$'))
                {
                    await _userBucket.AddAsync(subjectLogonId, new User(_4624.SubjectUserName, _4624.SubjectUserSid, _4624.Domain, subjectLogonId, logonType: _4624.LogonType.Parse(), _4624.UserPrivileged, _4624.IpAddress, sourceHostname: string.Empty, logon: Enum.GetName(Shared.Models.Console.Requests.Logon.Success)!, date), cancellationToken);
                }
            }

            if (!_4624.TargetUserSid.IsKnownSid() && TryGetTargetLogonId(_4624, out var targetLogonId))
            {
                await _workstationBucket.AddAsync(targetLogonId, new Workstation(_4624.WorkstationName, _4624.WorkstationIpAddress, _4624.Domain, date), cancellationToken);
                if (!_4624.TargetUserName.EndsWith('$'))
                {
                    await _userBucket.AddAsync(targetLogonId, new User(_4624.TargetUserName, _4624.TargetUserSid, _4624.Domain, targetLogonId, logonType: _4624.LogonType.Parse(), _4624.UserPrivileged, _4624.IpAddress, sourceHostname: string.Empty, logon: Enum.GetName(Shared.Models.Console.Requests.Logon.Success)!, date), cancellationToken);
                }
            }
        }
        else if (logonTracer is Logon4625 _4625)
        {
            var date = _4625.Date;
            await _userBucket.AddAsync(0, new User(_4625.TargetUserName, _4625.TargetUserSid, _4625.Domain, logonId: 0, logonType: _4625.LogonType.Parse(), privileged: _4625.UserPrivileged, _4625.IpAddress, sourceHostname: _4625.WorkstationName, logon: Enum.GetName(Shared.Models.Console.Requests.Logon.Failure)!, date), cancellationToken);
            await _workstationBucket.AddAsync(0, new Workstation(_4625.DomainControllerName, _4625.DomainControllerIpAddress, _4625.Domain, date), cancellationToken);
        }
    }

    private async ValueTask ProcessTracerAsync(ProcessTracer processTracer, CancellationToken cancellationToken)
    {
        if (processTracer is Process4688 _4688)
        {
            var date = _4688.Date;
            if (!_4688.SubjectUserSid.IsKnownSid() && TryGetSubjectLogonId(_4688, out var subjectLogonId))
            {
                if (!_4688.SubjectUserName.EndsWith('$'))
                {
                    var user = new User(_4688.SubjectUserName, _4688.SubjectUserSid, _4688.Domain, subjectLogonId, logonType: -1, _4688.UserPrivileged, sourceIp: string.Empty, sourceHostname: string.Empty, logon: Enum.GetName(Shared.Models.Console.Requests.Logon.Success)!, date);
                    if (!_userBucket.Contains(subjectLogonId, user))
                    {
                        await _userBucket.AddAsync(subjectLogonId, user, cancellationToken);
                    }
                }

                var workstation = new Workstation(_4688.WorkstationName, ipAddress: _4688.WorkstationIpAddress, _4688.Domain, date);
                if (!_workstationBucket.Contains(subjectLogonId, workstation))
                {
                    await _workstationBucket.AddAsync(subjectLogonId, workstation, cancellationToken);
                }

                await _processBucket.AddAsync(subjectLogonId, new Process(_4688.NewProcessName, _4688.NewProcessId, _4688.WorkstationName, _4688.SubjectUserName, _4688.SubjectUserSid, subjectLogonId, _4688.CommandLine, _4688.ParentProcessName, elevated: _4688.TokenElevationType.Equals(ElevatedTokenType), _4688.Domain, date), cancellationToken);
            }

            if (!_4688.TargetUserSid.IsKnownSid() && TryGetTargetLogonId(_4688, out var targetLogonId))
            {
                if (!_4688.TargetUserName.EndsWith('$'))
                {
                    var user = new User(_4688.TargetUserName, _4688.TargetUserSid, _4688.Domain, targetLogonId, logonType: -1, _4688.UserPrivileged, sourceIp: string.Empty, sourceHostname: string.Empty, logon: Enum.GetName(Shared.Models.Console.Requests.Logon.Success)!, date);
                    if (!_userBucket.Contains(targetLogonId, user))
                    {
                        await _userBucket.AddAsync(targetLogonId, user, cancellationToken);
                    }
                }

                var workstation = new Workstation(_4688.WorkstationName, ipAddress: _4688.WorkstationIpAddress, _4688.Domain, date);
                if (!_workstationBucket.Contains(targetLogonId, workstation))
                {
                    await _workstationBucket.AddAsync(targetLogonId, workstation, cancellationToken);
                }

                await _processBucket.AddAsync(targetLogonId, new Process(_4688.NewProcessName, _4688.NewProcessId, _4688.WorkstationName, _4688.TargetUserName, _4688.TargetUserSid, targetLogonId, _4688.CommandLine, _4688.ParentProcessName, elevated: _4688.TokenElevationType.Equals(ElevatedTokenType), _4688.Domain, date), cancellationToken);
            }
        }
    }
    
    private void ProcessTracer(NetworkTracer networkTracer)
    {
        _networkSeries.Insert(networkTracer);
    }
    
    private void ProcessTracer(SuccessLogonTracer logonTracer)
    {
        _logonSeries.Insert(logonTracer);
    }
    
    private void ProcessTracer(FailureLogonTracer logonTracer)
    {
        _logonSeries.Insert(logonTracer);
    }

    private static readonly Type[] ProcessingOrder = [typeof(LogonTracer), typeof(ProcessTracer), typeof(NetworkTracer), typeof(SuccessLogonTracer), typeof(FailureLogonTracer)];

    private static Type? GetTracerType(Tracer tracer)
    {
        if (tracer is LogonTracer) return typeof(LogonTracer);
        if (tracer is ProcessTracer) return typeof(ProcessTracer);
        if (tracer is NetworkTracer) return typeof(NetworkTracer);
        if (tracer is SuccessLogonTracer) return typeof(SuccessLogonTracer);
        if (tracer is FailureLogonTracer) return typeof(FailureLogonTracer);
        return null;
    }
    
    private async ValueTask ProcessAsync(IEnumerable<Tracer> tracers, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var group in tracers.GroupBy(GetTracerType).OrderBy(group => Array.IndexOf(ProcessingOrder, group.Key)))
            {
                if (group.Key == typeof(LogonTracer))
                {
                    foreach (var logonTracer in group.OfType<Logon4625>().Distinct())
                    {
                        await ProcessTracerAsync(logonTracer, cancellationToken);
                    }
                    
                    foreach (var logonTracer in group.OfType<Logon4624>())
                    {
                        await ProcessTracerAsync(logonTracer, cancellationToken);
                    }
                }
                else if (group.Key == typeof(ProcessTracer))
                {
                    foreach (var processTracer in group.OfType<ProcessTracer>())
                    {
                        await ProcessTracerAsync(processTracer, cancellationToken);
                    }
                }
                else if (group.Key == typeof(NetworkTracer))
                {
                    foreach (var networkTracer in group.OfType<NetworkTracer>())
                    {
                        ProcessTracer(networkTracer);
                    }
                }
                else if (group.Key == typeof(SuccessLogonTracer))
                {
                    foreach (var logonTracer in group.OfType<SuccessLogonTracer>())
                    {
                        ProcessTracer(logonTracer);
                    }
                }
                else if (group.Key == typeof(FailureLogonTracer))
                {
                    foreach (var logonTracer in group.OfType<FailureLogonTracer>())
                    {
                        ProcessTracer(logonTracer);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.Throttle(nameof(TracingStore), itself => itself.LogError(ex, "Could not process trace"), expiration: TimeSpan.FromMinutes(1));
        }
    }
    
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(InitializeAsync(cancellationToken), RemovePeriodicallyAsync(cancellationToken));
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

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(_eventSeries.InitializeAsync(cancellationToken), _networkSeries.InitializeAsync(cancellationToken));
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
    
    private async Task RemovePeriodicallyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    var processNamesByWorkstationName = new ConcurrentDictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in GetOutboundValues().Entries)
                    {
                        processNamesByWorkstationName.AddOrUpdate(entry.Computer.StripDomain().ToLowerInvariant(),
                            addValueFactory: _ => new HashSet<string> { Path.GetFileName(entry.ProcessName.ToLowerInvariant()) },
                            updateValueFactory: (_, current) =>
                            {
                                current.Add(Path.GetFileName(entry.ProcessName.ToLowerInvariant()));
                                return current;
                            });
                    }
                    
                    await _tracingRepository.RemoveExceptAsync(processNamesByWorkstationName, cancellationToken);
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

    public async ValueTask ProcessAsync(TraceContract contract, CancellationToken cancellationToken)
    {
        if (!await _processingBlock.SendAsync(contract, cancellationToken))
        {
            _logger.Throttle(nameof(TracingStore), itself => itself.LogError("Could not post trace"), expiration: TimeSpan.FromMinutes(1));
        }
    }

    public ValueTask ProcessAsync(EventContract contract, CancellationToken cancellationToken)
    {
        var eventThroughput = contract.EventCountById.Sum(kvp => kvp.Value);
        _metrics.Measure.Histogram.Update(MetricOptions.EventThroughput, eventThroughput);
        _eventSeries.Insert(contract.EventCountById.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        _streamingEventHub.Send(contract);
        return ValueTask.CompletedTask;
    }
    
    public ValueTask ProcessAsync(ProcessTreeContract processTreeContract, CancellationToken cancellationToken)
    {
        return _processTreeBucket.AddAsync(processTreeContract, cancellationToken);
    }

    public async Task<IEdgeListGraph<TracingNode, IEdge<TracingNode>>> TrySerializeGraph(TracingQuery tracingQuery, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        return await PopulateGraphAsync(tracingQuery, cts.Token);
    }
    
    public SortedDictionary<int, long> GetEventCountById()
    {
        return _eventSeries.GetEventCountById();
    }

    public (IEnumerable<OutboundEntry> Entries, IDictionary<string, long> OutboundByCountry) GetOutboundValues()
    {
        return _networkSeries.GetValues();
    }

    public IEnumerable<AccountLogon> EnumerateSuccessLogons()
    {
        return _logonSeries.EnumerateSuccessLogon();
    }

    public IEnumerable<AccountLogon> EnumerateFailureLogons()
    {
        return _logonSeries.EnumerateFailureLogon();
    }

    public async ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        await _processingBlock.DisposeAsync();
        await _eventSeries.DisposeAsync();
        await _networkSeries.DisposeAsync();
        await _logonSeries.DisposeAsync();
    }
}