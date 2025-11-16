using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Dataflow;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Databases.Abstractions.Domain.Processes;
using Collector.Databases.Implementation.Caching.LRU;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Collector.Services.Abstractions.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Agent.Processes;

public abstract class ProcessTreeService : IProcessTreeService
{
    private readonly ILogger<ProcessTreeService> _logger;
    private readonly IPropagatorBlock<ItemRemovedEventArgs<ProcessKey, ProcessRecord>, IList<ItemRemovedEventArgs<ProcessKey, ProcessRecord>>> _processingBlockPeriodic;
    private readonly ICache<ProcessKey, ProcessRecord> _lru;
    private readonly IDisposable _subscription;
    private static readonly Guid MicrosoftWindowsSecurityAuditingProviderGuid = new("54849625-5478-4994-A5BA-3E3B0328C30D");
    private const string NewProcessId = nameof(NewProcessId);
    private const string ProcessId = nameof(ProcessId);
    private const string NewProcessName = nameof(NewProcessName);
    private const string ParentProcessName = nameof(ParentProcessName);
    private const string SubjectLogonId = nameof(SubjectLogonId);
    private const string TargetLogonId = nameof(TargetLogonId);
    private const string SubjectUserSid = nameof(SubjectUserSid);
    private const string TargetUserSid = nameof(TargetUserSid);
    private readonly Timer _timer;

    protected ProcessTreeService(ILogger<ProcessTreeService> logger, IHostApplicationLifetime applicationLifetime, TimeSpan expiration)
    {
        _logger = logger;
        _processingBlockPeriodic = CreateDetectionBlock(applicationLifetime.ApplicationStopping, out var disposable);
        _subscription = disposable;
        _lru = Lrus.ProcessByKeyBuilder.WithExpireAfterWrite(expiration).Build();
        if (_lru.Events.Value is not null)
        {
            _lru.Events.Value.ItemRemoved += OnRemoved;
        }
        
        _timer = new Timer(_ =>
        {
            _lru.Policy.ExpireAfterWrite.Value?.TrimExpired();
        }, null, TimeSpan.Zero, expiration);
    }
    
    private void OnRemoved(object? sender, ItemRemovedEventArgs<ProcessKey, ProcessRecord> args)
    {
        if (!_processingBlockPeriodic.Post(args))
        {
            _logger.Throttle(nameof(ProcessTreeService), itself => itself.LogError("Could not post tree removal"), expiration: TimeSpan.FromMinutes(1));
        }
    }
    
    private IPropagatorBlock<ItemRemovedEventArgs<ProcessKey, ProcessRecord>, IList<ItemRemovedEventArgs<ProcessKey, ProcessRecord>>> CreateDetectionBlock(CancellationToken cancellationToken, out IDisposable disposableLink)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            SingleProducerConstrained = false,
            BoundedCapacity = DataflowBlockOptions.Unbounded,
            CancellationToken = cancellationToken
        };

        var bufferBlock = DataFlowHelper.CreateUnboundedPeriodicBlock<ItemRemovedEventArgs<ProcessKey, ProcessRecord>>(TimeSpan.FromSeconds(1), count: 1000);
        var options = new DataflowLinkOptions { PropagateCompletion = true };
        var propagationBlock = new ActionBlock<IList<ItemRemovedEventArgs<ProcessKey, ProcessRecord>>>(Process, executionDataflow);
        disposableLink = bufferBlock.LinkTo(propagationBlock, options);
        return bufferBlock;
    }

    private void Process(IList<ItemRemovedEventArgs<ProcessKey, ProcessRecord>> args)
    {
        foreach (var arg in args)
        {
            if (arg.Value?.Parent is null) continue;
            if (string.IsNullOrEmpty(arg.Value.Parent.Pname)) continue;
            SendTree(new ProcessKey(arg.Key.WorkstationName, arg.Key.Domain, arg.Key.ProcessId, processName: arg.Key.ProcessName, logonId: arg.Key.LogonId), new ProcessTree(arg.Value.ToString(), arg.Key.ProcessName));
        }
    }

    public void Add(string workstationName, string domain, Guid providerGuid, uint eventId, IDictionary<string, string> eventData)
    {
        if (TryGetCreatedProcess(providerGuid, eventId, eventData, out var processId, out var parentProcessId, out var processName, out var parentProcessName, out var logonId))
        {
            var logonIdValue = logonId.ParseLong();
            var processKey = new ProcessKey(workstationName, domain, processId, processName, logonIdValue);
            var process = new ProcessRecord(processId, processName)
            {
                Ppid = parentProcessId,
                Parent = _lru.GetOrAdd(new ProcessKey(workstationName, domain, parentProcessId, parentProcessName, logonIdValue), valueFactory: _ => CreateFakeParentProcess(parentProcessId, parentProcessName))
            };

            _lru.AddOrUpdate(processKey, process);
        }
    }

    private static bool TryGetCreatedProcess(Guid providerGuid, uint eventId, IDictionary<string, string> eventData, out uint processId, out uint parentProcessId, [MaybeNullWhen(false)] out string processName, [MaybeNullWhen(false)] out string parentProcessName, [MaybeNullWhen(false)] out string logonId)
    {
        processId = 0;
        parentProcessId = 0;
        processName = null;
        parentProcessName = null;
        logonId = null;
        if (MicrosoftWindowsSecurityAuditingProviderGuid == providerGuid && eventId == 4688)
        {
            var success = true;
            if (eventData.TryGetValue(NewProcessId, out var pid))
            {
                processId = pid.ParseUnsigned();
            }
            else
            {
                success = false;
            }
            
            if (eventData.TryGetValue(ProcessId, out var ppid))
            {
                parentProcessId = ppid.ParseUnsigned();
            }
            else
            {
                success = false;
            }

            success &= eventData.TryGetValue(NewProcessName, out processName);
            success &= eventData.TryGetValue(ParentProcessName, out parentProcessName);
            success &= eventData.TryGetValue(SubjectLogonId, out var subjectLogonId);
            success &= eventData.TryGetValue(TargetLogonId, out var targetLogonId);
            success &= eventData.TryGetValue(SubjectUserSid, out var subjectUserSid);
            success &= eventData.TryGetValue(TargetUserSid, out var targetUserSid);
            if (!string.IsNullOrEmpty(subjectUserSid) && !string.IsNullOrEmpty(targetUserSid) && subjectUserSid.IsKnownSid() && targetUserSid.IsKnownSid()) return false;
            if (!string.IsNullOrEmpty(subjectUserSid) && subjectUserSid.IsKnownSid())
            {
                logonId = targetLogonId;
                return success;
            }
            
            if (!string.IsNullOrEmpty(targetUserSid) && targetUserSid.IsKnownSid())
            {
                logonId = subjectLogonId;
                return success;
            }
        }

        return false;
    }

    protected abstract void SendTree(ProcessKey key, ProcessTree processTree);

    private static ProcessRecord CreateFakeParentProcess(uint pid, string name)
    {
        return new ProcessRecord(pid, name);
    }

    public async ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        _processingBlockPeriodic.Complete();
        await _timer.DisposeAsync();
    }
}