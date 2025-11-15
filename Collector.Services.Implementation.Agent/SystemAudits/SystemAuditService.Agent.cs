using Collector.Core.Hubs.SystemAudits;
using Collector.Databases.Implementation.Caching.LRU;
using Collector.Services.Implementation.SystemAudits;

namespace Collector.Services.Implementation.Agent.SystemAudits;

public sealed class SystemAuditServiceAgent(IStreamingSystemAuditHub streamingSystemAuditHub)
    : SystemAuditService
{
    protected override void ExecuteCore()
    {
        Lrus.AuditStatusByKey.Policy.ExpireAfterWrite.Value?.TrimExpired();
        foreach (var item in Lrus.AuditStatusByKey)
        {
            if (!TryGetContract(item, out var systemAuditContract)) continue;
            streamingSystemAuditHub.SendSystemAudit(systemAuditContract);
        }
    }
}