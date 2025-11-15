using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.afc0e7da_4e96_1953_3fa3_8e9112c06c1c;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(afc0e7da_4e96_1953_3fa3_8e9112c06c1c.YamlRule.Yaml)
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
            { "CommandLine", "C:\\WINDOWS\\system32\\cmd.exe netstat -an | find \"8888\"" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "Cmdline: C:\\WINDOWS\\system32\\cmd.exe netstat -an | find \"8888\" \u00a6 Proc: N/A \u00a6 PID: N/A \u00a6 User: N/A \u00a6 LID: N/A";
    }
}