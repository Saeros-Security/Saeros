using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._37e4024a_6c80_4d8f_b95d_2e7e94f3a8d1;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(_37e4024a_6c80_4d8f_b95d_2e7e94f3a8d1.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "5156" },
            { WinEventExtensions.ChannelKey, "Security" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.ProviderGuidKey, "54849625-5478-4994-A5BA-3E3B0328C30D" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Application", "C:\\Windows\\System32\\dialer.exe" },
            { "Direction", "%%14593" },
            { "DestAddress", "127.0.0.1" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = false;
    }
}