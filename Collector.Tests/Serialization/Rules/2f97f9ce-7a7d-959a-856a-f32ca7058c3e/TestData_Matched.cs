using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._2f97f9ce_7a7d_959a_856a_f32ca7058c3e;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(_2f97f9ce_7a7d_959a_856a_f32ca7058c3e.YamlRule.Yaml)
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
            { "NewProcessName", "C:\\Windows\\System32\\taskkill.exe" },
            { "CommandLine", "TASKKILL /PID 11632 /f" },
            { "ParentProcessName", "C:\\Windows\\System32\\cmd.exe" }
        };
            
        Add(new WinEvent(system, eventData));
        
        Match = true;
        Details = "Cmdline: TASKKILL /PID 11632 /f ¦ Proc: C:\\Windows\\System32\\taskkill.exe ¦ PID: N/A ¦ User: N/A ¦ LID: N/A";
    }
}