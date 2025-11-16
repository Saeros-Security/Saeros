using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._308a3356_4624_7c95_24df_cf5a02e5eb56;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(_308a3356_4624_7c95_24df_cf5a02e5eb56.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "5145" },
            { WinEventExtensions.ChannelKey, "Security" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.ProviderGuidKey, "54849625-5478-4994-A5BA-3E3B0328C30D" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ShareName", "\\\\*\\IPC$" },
            { "RelativeTargetName", "Collector" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "SrcUser: N/A ¦ ShareName: \\\\*\\IPC$ ¦ SharePath: N/A ¦ Path: Collector ¦ SrcIP: N/A ¦ LID: N/A";
    }
}