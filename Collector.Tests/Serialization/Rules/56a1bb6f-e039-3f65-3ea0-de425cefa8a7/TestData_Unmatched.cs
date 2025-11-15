using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._56a1bb6f_e039_3f65_3ea0_de425cefa8a7;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(_56a1bb6f_e039_3f65_3ea0_de425cefa8a7.YamlRule.Yaml)
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
            { "IpAddress", "192.168.1.45" }
        };

        Add(new WinEvent(system, eventData));
        Match = false;
    }
}