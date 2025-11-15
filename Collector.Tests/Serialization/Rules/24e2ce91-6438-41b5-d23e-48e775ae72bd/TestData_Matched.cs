using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._24e2ce91_6438_41b5_d23e_48e775ae72bd;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(_24e2ce91_6438_41b5_d23e_48e775ae72bd.YamlRule.Yaml)
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
            { "ParentProcessName", "C:\\Program Files\\JetBrains\\ETW Host\\16\\JetBrains.Etw.Collector.Host.exe" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "Cmdline: N/A \u00a6 Proc: C:\\Windows\\Temp\\jetbrainsproc_d14935c8-0e24-4596-8aff-3e9b0ffd8f6c\\JetBrains.Dpa.Collector.exe \u00a6 PID: N/A \u00a6 User: N/A \u00a6 LID: N/A";
    }
}