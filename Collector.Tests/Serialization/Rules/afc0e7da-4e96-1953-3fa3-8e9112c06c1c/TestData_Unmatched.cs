using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.afc0e7da_4e96_1953_3fa3_8e9112c06c1c;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(afc0e7da_4e96_1953_3fa3_8e9112c06c1c.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "4688" },
            { WinEventExtensions.ChannelKey, "Security" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.ProviderGuidKey, "54849625-5478-4994-A5BA-3E3B0328C30D" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "CommandLine", "C:\\WINDOWS\\system32\\svchost.exe -k LocalSystemNetworkRestricted -p -s NgcSvc" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = false;
    }
}