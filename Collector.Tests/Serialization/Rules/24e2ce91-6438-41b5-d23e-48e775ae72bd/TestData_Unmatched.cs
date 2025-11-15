using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._24e2ce91_6438_41b5_d23e_48e775ae72bd;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(_24e2ce91_6438_41b5_d23e_48e775ae72bd.YamlRule.Yaml)
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
            { "NewProcessName", "C:\\Windows\\Temp\\jetbrainsproc_d14935c8-0e24-4596-8aff-3e9b0ffd8f6c\\JetBrains.Dpa.Collector.exe" },
            { "ParentProcessName", "C:\\Windows\\System32\\dxgiadaptercache.exe" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = false;
    }
}