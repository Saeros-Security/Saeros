using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.SpecialKeywords;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(SpecialKeywords.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "7045" },
            { WinEventExtensions.ChannelKey, "System" },
            { WinEventExtensions.ProviderNameKey, "Service Control Manager" },
            { WinEventExtensions.ProviderGuidKey, "555908d1-a6d7-4695-8e1e-26931d2012f4" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ServiceName", "malicious-service" },
            { "ImagePath", "C:\\AppData\\Local\\Temp\\GUM\\.tmp\\goopdate.dll" }
        };

        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "Svc: malicious-service \u00a6 Path: C:\\AppData\\Local\\Temp\\GUM\\.tmp\\goopdate.dll \u00a6 Acct: N/A \u00a6 StartType: N/A";
    }
}