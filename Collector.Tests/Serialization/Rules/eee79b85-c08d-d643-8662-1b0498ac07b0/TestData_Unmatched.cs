using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.eee79b85_c08d_d643_8662_1b0498ac07b0;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(eee79b85_c08d_d643_8662_1b0498ac07b0.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "10" },
            { WinEventExtensions.ChannelKey, "Microsoft-Windows-Sysmon/Operational" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Sysmon" },
            { WinEventExtensions.ProviderGuidKey, "5770385F-C22A-43E0-BF4C-06F5698FFBD9" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "TargetImage", "C:\\Windows\\system32\\lsass.exe" },
            { "CallTrace", "C:\\Windows\\SYSTEM32\\ntdll.dll+ C:\\Windows\\System32\\KERNELBASE.dll+ libffi-7.dll _ctypes.pyd+ python27.dll+" },
            { "GrantedAccess", "0x1FFFFF" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = false;
    }
}