using System.Globalization;
using Collector.ActiveDirectory.AuditPolicies;
using Collector.ActiveDirectory.Extensions;
using Collector.ActiveDirectory.Helpers.AuditPolicies.Csv;
using CsvHelper;
using Polly;
using Polly.Retry;
using static Vanara.PInvoke.AdvApi32;

namespace Collector.ActiveDirectory.Helpers.AuditPolicies;

public static class DomainAuditPolicyHelper
{
    private static readonly RetryPolicy IoPolicy = Policy.Handle<IOException>().WaitAndRetry(3, sleepDurationProvider: _ => TimeSpan.FromSeconds(1));

    public static void SetAuditPolicies(string rootPath, IDictionary<Guid, POLICY_AUDIT_EVENT_OPTIONS> auditOptionBySubcategory, bool overrideAuditPolicies, CancellationToken cancellationToken)
    {
        var auditPath = $@"{rootPath}\Machine\Microsoft\Windows NT\Audit";
        var csvAuditPath = $@"{auditPath}\Audit.csv";

        var audits = new List<AuditPolicyCsv>();
        if (overrideAuditPolicies)
        {
            foreach (var auditOption in auditOptionBySubcategory)
            {
                if (AuditPolicyMapping.SubcategoryNameByGuid.TryGetValue(auditOption.Key, out var name))
                {
                    audits.Add(new AuditPolicyCsv(MachineName: string.Empty, PolicyTarget: "System", Subcategory: name, SubcategoryGuid: auditOption.Key.ToString("B"), InclusionSetting: auditOption.Value.Stringify(), ExclusionSetting: string.Empty, SettingValue: auditOption.Value.ToValue()));
                }
            }
        }
        
        IoPolicy.Execute(() => Directory.CreateDirectory(auditPath));
        IoPolicy.Execute(() =>
        {
            using var writer = new StreamWriter(csvAuditPath, append: false);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.Context.RegisterClassMap<AuditPolicyCsvMap>();
            csv.WriteRecords(audits);
        });
    }
}