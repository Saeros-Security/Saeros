using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace Collector.ActiveDirectory.Helpers.AuditPolicies.Csv;

public record AuditPolicyCsv([Index(0)] string MachineName, [Index(1)] string PolicyTarget, [Index(2)] string Subcategory, [Index(3)] string SubcategoryGuid, [Index(4)] string InclusionSetting, [Index(5)] string ExclusionSetting, [Index(6)] string SettingValue);

public sealed class AuditPolicyCsvMap : ClassMap<AuditPolicyCsv>
{
    public AuditPolicyCsvMap()
    {
        Map(m => m.MachineName).Name("Machine Name");
        Map(m => m.PolicyTarget).Name("Policy Target");
        Map(m => m.Subcategory).Name("Subcategory");
        Map(m => m.SubcategoryGuid).Name("Subcategory GUID");
        Map(m => m.InclusionSetting).Name("Inclusion Setting");
        Map(m => m.ExclusionSetting).Name("Exclusion Setting");
        Map(m => m.SettingValue).Name("Setting Value");
    }
}