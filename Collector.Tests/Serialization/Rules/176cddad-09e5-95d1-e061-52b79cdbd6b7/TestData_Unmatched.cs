using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._176cddad_09e5_95d1_e061_52b79cdbd6b7;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(_176cddad_09e5_95d1_e061_52b79cdbd6b7.YamlRule.Yaml)
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
            { "CommandLine", "\"C:\\Users\\Benjamin\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe\" \"C:\\Users\\Benjamin\\Documents\\Saeros\\collector\\Collector\\Collector.csproj\"" },
            { "ParentProcessName", "C:\\Windows\\explorer.exe" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = false;
    }
}