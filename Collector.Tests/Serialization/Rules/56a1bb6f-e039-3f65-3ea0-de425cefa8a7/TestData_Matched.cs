using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._56a1bb6f_e039_3f65_3ea0_de425cefa8a7;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(_56a1bb6f_e039_3f65_3ea0_de425cefa8a7.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "4624" },
            { WinEventExtensions.ChannelKey, "Security" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.ProviderGuidKey, "54849625-5478-4994-a5ba-3e3b0328c30d" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "LogonType", "10" },
            { "IpAddress", "172.188.244.61" }
        };

        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "Type: 10 - REMOTE INTERACTIVE \u00a6 TgtUser: N/A \u00a6 SrcComp: N/A \u00a6 SrcIP: 172.188.244.61 \u00a6 LID: N/A";
    }
}