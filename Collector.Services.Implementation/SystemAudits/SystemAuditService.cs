using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;
using BitFaster.Caching;
using Collector.Core.Services;
using Collector.Core.SystemAudits;
using Collector.Databases.Implementation.Caching.LRU;
using ConcurrentCollections;
using Streaming;

namespace Collector.Services.Implementation.SystemAudits;

public abstract class SystemAuditService : ISystemAuditService
{
    private sealed record AuditTimeStamp(AuditStatus Status, DateTimeOffset TimeStamp);
    private readonly Subject<string> _serverConnected = new();
    private readonly ConcurrentDictionary<SystemAuditKey, AuditTimeStamp> _timeStampByAuditKey = new();
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _serversByDomain = new(StringComparer.OrdinalIgnoreCase);

    protected SystemAuditService()
    {
        if (Lrus.AuditStatusByKey.Events.Value is not null)
        {
            Lrus.AuditStatusByKey.Events.Value.ItemRemoved += OnEvicted;
        }
    }
    
    protected abstract void ExecuteCore();
    
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            ExecuteCore();
        }
    }

    private void OnEvicted(object? sender, ItemRemovedEventArgs<SystemAuditKey, AuditStatus> args)
    {
        if (args.Reason == ItemRemovedReason.Removed) return;
        if (args.Reason == ItemRemovedReason.Cleared) return;
        Lrus.AuditStatusByKey.AddOrUpdate(args.Key, AuditStatus.Success);
        UpdateTimestamp(args.Key, AuditStatus.Success);
    }
    
    public bool TryGetContract(KeyValuePair<SystemAuditKey, AuditStatus> pair, [MaybeNullWhen(false)] out SystemAuditContract systemAuditContract)
    {
        systemAuditContract = null;
        if (TryGetNameExplanation(pair.Key, pair.Value, out var name, out var explanation))
        {
            systemAuditContract = new SystemAuditContract
            {
                Date = _timeStampByAuditKey.TryGetValue(pair.Key, out var value) ? value.TimeStamp.Ticks : DateTimeOffset.UtcNow.Ticks,
                Status = pair.Value,
                Name = name,
                Explanation = explanation
            };

            return true;
        }

        return false;
    }

    public bool TryGetNameExplanation(SystemAuditKey key, AuditStatus status, [MaybeNullWhen(false)] out string name, [MaybeNullWhen(false)] out string explanation)
    {
        name = null;
        explanation = null;
        switch (key.SystemAuditType)
        {
            case SystemAuditType.DetectionIngestion:
                name = "Detection ingestion";
                explanation = status == AuditStatus.Success ? "The system hasn't reported any detection loss due to performance problems." : "The system has detected a detection loss.";
                return true;
            case SystemAuditType.AuditPolicies:
                name = "Audit Policies";
                explanation = status == AuditStatus.Success ? "Required audit policies are automatically configured based on your enabled rules." : "Audit policies are not automatically updated and may prevent rules from working as expected.";
                return true;
            case SystemAuditType.DomainController:
                if (string.IsNullOrEmpty(key.Details))
                {
                    name = "Domain controllers discovery";
                    explanation = status == AuditStatus.Success ? "The system was able to retrieve domain controllers." : "The system could not retrieve domain controllers.";
                    return true;
                }

                if (_serversByDomain.Any(kvp => kvp.Value.Contains(key.Details)))
                {
                    name = $"RPC communication with server {key.Details}";
                    explanation = status == AuditStatus.Success ? $"The system hasn't reported any RPC communication issues with server: {key.Details}." : $"The system has detected an RPC communication outage with domain controller: {key.Details}.";
                    return true;
                }

                return false;
            case SystemAuditType.Collector:
                name = "Communication with collectors";
                explanation = status == AuditStatus.Success ? "The system was able to communicate with collectors." : "The system could not communicate with collectors.";
                return true;
            case SystemAuditType.Integration:
                name = $"Communication with integration {key.Details}";
                explanation = status == AuditStatus.Success ? $"The system hasn't reported any communication issues with integration {key.Details}." : $"The system has detected a communication outage with integration {key.Details}.";
                return true;
            default:
                return false;
        }
    }

    public void Add(SystemAuditKey key, AuditStatus status)
    {
        Lrus.AuditStatusByKey.AddOrUpdate(key, status);
        UpdateTimestamp(key, status);
    }

    public void ServerConnected(string domain, string serverName)
    {
        _serverConnected.OnNext(serverName);
        _serversByDomain.AddOrUpdate(domain, addValue: new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase) { serverName }, updateValueFactory: (_, current) =>
        {
            current.Add(serverName);
            return current;
        });
    }

    public void DeleteDomain(string domain)
    {
        _serversByDomain.Remove(domain, out _);
        Lrus.AuditStatusByKey.Clear();
    }

    public IObservable<string> OnServerConnected => _serverConnected;

    private void UpdateTimestamp(SystemAuditKey key, AuditStatus status)
    {
        if (_timeStampByAuditKey.TryGetValue(key, out var auditTimeStamp))
        {
            if (auditTimeStamp.Status == status)
            {
                return;
            }
        }

        _timeStampByAuditKey[key] = new AuditTimeStamp(status, DateTimeOffset.UtcNow);
    }
    
    public void Dispose()
    {
        _serverConnected.Dispose();
    }
}