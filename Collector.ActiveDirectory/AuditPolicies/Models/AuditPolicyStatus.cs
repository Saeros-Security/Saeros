namespace Collector.ActiveDirectory.AuditPolicies.Models;

[Flags]
public enum AuditPolicyStatus
{
    Success = 1,
    Failure = 2
}