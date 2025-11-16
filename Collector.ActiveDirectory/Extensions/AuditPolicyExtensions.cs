using System.Runtime.Versioning;
using Collector.ActiveDirectory.AuditPolicies.Models;
using Vanara.PInvoke;

namespace Collector.ActiveDirectory.Extensions;

[SupportedOSPlatform("windows")]
public static class AuditPolicyExtensions
{
    public static AdvApi32.POLICY_AUDIT_EVENT_OPTIONS ToOptions(this AuditPolicyStatus auditPolicyStatus)
    {
        if (auditPolicyStatus == AuditPolicyStatus.Success)
        {
            return AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS;
        }

        if (auditPolicyStatus == AuditPolicyStatus.Failure)
        {
            return AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE;
        }

        if (auditPolicyStatus.HasFlag(AuditPolicyStatus.Success) && auditPolicyStatus.HasFlag(AuditPolicyStatus.Failure))
        {
            return AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS | AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE;
        }

        return AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED;
    }
    
    public static string ToValue(this AdvApi32.POLICY_AUDIT_EVENT_OPTIONS options)
    {
        return options switch
        {
            AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS => "1",
            AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE => "2",
            AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS | AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE => "3",
            _ => string.Empty
        };
    }

    public static string Stringify(this AdvApi32.POLICY_AUDIT_EVENT_OPTIONS options)
    {
        return options switch
        {
            AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_NONE => "No Auditing",
            AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS => "Success",
            AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE => "Failure",
            AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED => "Unchanged",
            AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS | AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE => "Success and Failure",
            _ => string.Empty
        };
    }
}