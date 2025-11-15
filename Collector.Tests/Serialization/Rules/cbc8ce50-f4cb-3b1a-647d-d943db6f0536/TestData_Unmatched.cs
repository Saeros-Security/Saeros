using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.cbc8ce50_f4cb_3b1a_647d_d943db6f0536;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(cbc8ce50_f4cb_3b1a_647d_d943db6f0536.YamlRule.Yaml)
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
            { "Image", "C:\\Windows\\System32\\explorer.exe" },
            { "ParentCommandLine", "cmd.exe /c RoamDiag.cmd -outputpath" }
        };

        Add(new WinEvent(system, eventData));
        Match = false;
    }
}