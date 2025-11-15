using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.f224a2b6_2db1_a1a2_42d4_25df0c460915;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(f224a2b6_2db1_a1a2_42d4_25df0c460915.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "16" },
            { WinEventExtensions.ChannelKey, "System" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Kernel-General" },
            { WinEventExtensions.ProviderGuidKey, "a68ca8b7-004f-d7b6-a698-07e2de0f1f5d" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Property", "C:\\AppData\\Local\\TEST\\SAM-" },
            { "Property2", ".dmp" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "";
    }
}