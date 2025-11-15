using Collector.Core.Hubs.SystemAudits;
using Collector.Core.SystemAudits;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Databases.Implementation.Caching.LRU;
using Collector.Services.Implementation.SystemAudits;
using Streaming;

namespace Collector.Services.Implementation.Bridge.SystemAudits;

public sealed class SystemAuditServiceBridge(IStreamingSystemAuditHub streamingSystemAuditHub, ISettingsStore settingsStore)
    : SystemAuditService
{
    protected override void ExecuteCore()
    {
        if (TryGetContract(new KeyValuePair<SystemAuditKey, AuditStatus>(new SystemAuditKey(SystemAuditType.AuditPolicies), settingsStore.OverrideAuditPolicies ? AuditStatus.Success : AuditStatus.Warning), out var contract))
        {
            streamingSystemAuditHub.SendSystemAudit(contract);
        }
        
        Lrus.AuditStatusByKey.Policy.ExpireAfterWrite.Value?.TrimExpired();
        foreach (var item in Lrus.AuditStatusByKey)
        {
            if (!TryGetContract(item, out var systemAuditContract)) continue;
            streamingSystemAuditHub.SendSystemAudit(systemAuditContract);
        }
    }
}