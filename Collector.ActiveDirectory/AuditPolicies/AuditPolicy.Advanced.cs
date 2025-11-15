using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Collector.ActiveDirectory.AuditPolicies.Models;
using Collector.ActiveDirectory.Extensions;
using static Vanara.PInvoke.AdvApi32;

namespace Collector.ActiveDirectory.AuditPolicies;

[SupportedOSPlatform("windows")]
public static class AuditPolicyAdvanced
{
    public static HashSet<AuditPolicyCategory> QueryAdvancedAuditPolicies()
    {
        var auditPolicies = new HashSet<AuditPolicyCategory>();
        foreach (var categoryGuid in AuditEnumerateCategories())
        {
            var subCategories = new HashSet<AuditPolicySubCategory>();
            foreach (var subcategoryGuid in AuditEnumerateSubCategories(categoryGuid))
            {
                subCategories.Add(new AuditPolicySubCategory(subcategoryGuid));
            }

            auditPolicies.Add(new AuditPolicyCategory(categoryGuid, subCategories));
        }

        return auditPolicies;
    }
    
    public static IDictionary<Guid, POLICY_AUDIT_EVENT_OPTIONS> GetAuditOptionBySubcategory(ISet<int> eventIds)
    {
        var advancedAuditPolicies = QueryAdvancedAuditPolicies();
        var auditPolicyMapping = AuditPolicyMapping.EventIdBySubcategoryGuid;
        var optionBySubCategory = new ConcurrentDictionary<Guid, POLICY_AUDIT_EVENT_OPTIONS>();
        foreach (var advancedAuditPolicy in advancedAuditPolicies)
        {
            foreach (var subCategory in advancedAuditPolicy.SubCategories)
            {
                if (auditPolicyMapping.TryGetValue(subCategory.SubCategoryGuid, out var auditPolicyEventIds))
                {
                    foreach (var eventId in eventIds)
                    {
                        var value = auditPolicyEventIds.FirstOrDefault(auditEventId => auditEventId.EventId == eventId);
                        if (value == null) continue;
                        var options = value.Status.ToOptions();
                        optionBySubCategory.AddOrUpdate(subCategory.SubCategoryGuid, addValue: options, updateValueFactory: (_, current) =>
                        {
                            if (options.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS))
                            {
                                current |= POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS;
                            }

                            if (options.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE))
                            {
                                current |= POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE;
                            }

                            return current;
                        });
                    }
                }
            }
        }

        return optionBySubCategory;
    }
}