using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using static Vanara.PInvoke.AdvApi32;

namespace Collector.ActiveDirectory.AuditPolicies;

[SupportedOSPlatform("windows")]
public static class AuditPolicyBasic
{
    public static Dictionary<Guid, POLICY_AUDIT_EVENT_OPTIONS> QueryBasicAuditPolicies(ILogger logger)
    {
        var basicAuditPolicies = new Dictionary<Guid, POLICY_AUDIT_EVENT_OPTIONS>
        {
            { new Guid("{69979848-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED }, // SYSTEM
            { new Guid("{69979849-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED }, // LOGON
            { new Guid("{6997984A-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED }, // OBJECT ACCESS
            { new Guid("{6997984B-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED }, // PRIVILEGE USE
            { new Guid("{6997984C-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED }, // DETAILED TRACKING
            { new Guid("{6997984D-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED }, // POLICY CHANGE
            { new Guid("{6997984E-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED }, // ACCOUNT MANAGEMENT
            { new Guid("{6997984F-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED }, // DIRECTORY SERVICE ACCESS
            { new Guid("{69979850-797A-11D9-BED3-505054503030}"), POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED } // ACCOUNT LOGON
        };

        var systemName = new LSA_UNICODE_STRING();
        var attributes = new LSA_OBJECT_ATTRIBUTES
        {
            Length = 0,
            RootDirectory = IntPtr.Zero,
            Attributes = 0,
            SecurityDescriptor = IntPtr.Zero,
            SecurityQualityOfService = IntPtr.Zero
        };

        var status = LsaOpenPolicy(systemName, attributes, LsaPolicyRights.POLICY_VIEW_AUDIT_INFORMATION, out var handle);
        if (status.Succeeded)
        {
            var audit = LsaQueryInformationPolicy<POLICY_AUDIT_EVENTS_INFO>(handle);
            for (var i = 0; i < audit.MaximumAuditEventCount; i++)
            {
                var categoryGuid = basicAuditPolicies.ElementAt(i).Key;
                if (audit.EventAuditingOptions == null)
                {
                    logger.LogError("Could not fetch audit option for category {Guid}", categoryGuid);
                    continue;
                }

                var option = audit.EventAuditingOptions[i];
                basicAuditPolicies[categoryGuid] = option;
            }
        }
        else
        {
            logger.LogError("Could not fetch audit policies");
        }

        return basicAuditPolicies;
    }
}