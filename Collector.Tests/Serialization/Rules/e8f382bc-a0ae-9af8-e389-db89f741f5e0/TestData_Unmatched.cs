using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.e8f382bc_a0ae_9af8_e389_db89f741f5e0;

public class TestData_Unmatched : TestData
{
    public TestData_Unmatched() : base(e8f382bc_a0ae_9af8_e389_db89f741f5e0.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "7" },
            { WinEventExtensions.ChannelKey, "Microsoft-Windows-Sysmon/Operational" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Sysmon" },
            { WinEventExtensions.ProviderGuidKey, "5770385F-C22A-43E0-BF4C-06F5698FFBD9" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ImageLoaded", "C:\\AppData\\Local\\Temp\\GUM\\.tmp\\\\goopdate.dll" },
            { "Image", "C:\\AppData\\Local\\Temp\\GUM\\.tmp\\origin\\goopdate.dll" }
        };

        Add(new WinEvent(system, eventData));
        Match = false;
    }
}