using System.Diagnostics.CodeAnalysis;

namespace Collector.Databases.Abstractions.Repositories.AuditPolicies;

public interface IAuditPoliciesRepository
{
    void AddBackup(byte[] content, bool advancedAuditPoliciesEnabled);

    bool TryGetBackup([MaybeNullWhen(false)] out byte[] content, out bool advancedAuditPoliciesEnabled);
}