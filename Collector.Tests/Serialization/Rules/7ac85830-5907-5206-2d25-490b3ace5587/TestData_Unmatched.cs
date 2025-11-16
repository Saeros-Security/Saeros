using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._7ac85830_5907_5206_2d25_490b3ace5587;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(_7ac85830_5907_5206_2d25_490b3ace5587.YamlRule.Yaml)
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
            { "Direction", "%%14593" },
            { "DestPort", "7777" },
            { "DestAddress", "178.219.91.204" },
            { "Application", "\\device\\harddiskvolume4\\program files\\transmission\\transmission-qt.exe" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = false;
    }
}