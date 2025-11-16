using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._95a0be22_3e44_b097_f78c_c64a5a6dd761;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(_95a0be22_3e44_b097_f78c_c64a5a6dd761.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "1" },
            { WinEventExtensions.ChannelKey, "Microsoft-Windows-Sysmon/Operational" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Sysmon" },
            { WinEventExtensions.ProviderGuidKey, "5770385F-C22A-43E0-BF4C-06F5698FFBD9" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Image", "C:\\Windows\\System32\\cmd.exe" },
            { "OriginalFileName", "Cmd.exe" },
            { "CommandLine", "directory-s" },
        };
            
        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "Cmdline: directory-s \u00a6 Proc: C:\\Windows\\System32\\cmd.exe \u00a6 User: N/A \u00a6 ParentCmdline: N/A \u00a6 LID: N/A \u00a6 LGUID: N/A \u00a6 PID: N/A \u00a6 PGUID: N/A \u00a6 ParentPID: N/A \u00a6 ParentPGUID: N/A \u00a6 Description: N/A \u00a6 Product: N/A \u00a6 Company: N/A \u00a6 Hashes: N/A";
    }
}