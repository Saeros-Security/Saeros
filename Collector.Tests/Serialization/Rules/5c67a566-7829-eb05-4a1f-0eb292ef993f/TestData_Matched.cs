using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._5c67a566_7829_eb05_4a1f_0eb292ef993f;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(_5c67a566_7829_eb05_4a1f_0eb292ef993f.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "4624" },
            { WinEventExtensions.ChannelKey, "Security" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.ProviderGuidKey, "54849625-5478-4994-A5BA-3E3B0328C30D" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "IpAddress", "112.10.19.29" },
            { "LogonType", "3" },
            { "TargetLogonId", "0x3109aa" },
            { "TargetUserName", "BENJAMINBOU8814$" },
            { "WorkstationName", "-" }
        };
            
        Add(new WinEvent(system, eventData));

        Match = true;
        Details = "Type: 3 - NETWORK \u00a6 TgtUser: BENJAMINBOU8814$ \u00a6 SrcComp: - \u00a6 SrcIP: 112.10.19.29 \u00a6 LID: 0x3109aa";
    }
}