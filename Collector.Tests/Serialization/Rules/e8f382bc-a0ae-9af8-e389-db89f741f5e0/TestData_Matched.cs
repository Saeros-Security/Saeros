using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.e8f382bc_a0ae_9af8_e389_db89f741f5e0;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(e8f382bc_a0ae_9af8_e389_db89f741f5e0.YamlRule.Yaml)
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
            { "Image", "C:\\AppData\\Local\\Temp\\GUM\\.tmp\\goopdate.dll" }
        };

        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "Image: C:\\AppData\\Local\\Temp\\GUM\\.tmp\\\\goopdate.dll \u00a6 Proc: C:\\AppData\\Local\\Temp\\GUM\\.tmp\\goopdate.dll \u00a6 Description: N/A \u00a6 Product: N/A \u00a6 Company: N/A \u00a6 Signed: N/A \u00a6 Sig: N/A \u00a6 PID: N/A \u00a6 PGUID: N/A \u00a6 Hash: N/A \u00a6 OrigFilename: N/A";
    }
}