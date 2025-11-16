using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.f2b2d6f5_92ed_d0f5_25fe_38019bd55906;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(f2b2d6f5_92ed_d0f5_25fe_38019bd55906.YamlRule.Yaml)
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
            { "NewProcessName", "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe" },
            { "CommandLine", "powershell.exe  -NoProfile -Command \"& {if($PSVersionTable.PSVersion.Major -ge 3){Import-Module 'C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\Common7\\Tools\\Microsoft.VisualStudio.DevShell.dll'; Send-VsDevShellTelemetry -NewInstanceType Cmd; }}\" " },
            { "ParentProcessName", "C:\\Windows\\System32\\cmd.exe" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = false;
    }
}