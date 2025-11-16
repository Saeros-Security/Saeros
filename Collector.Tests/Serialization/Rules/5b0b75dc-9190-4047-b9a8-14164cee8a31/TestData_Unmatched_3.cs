using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._5b0b75dc_9190_4047_b9a8_14164cee8a31;

public class TestData_Unmatched_3 : TestData
{
    public TestData_Unmatched_3() : base(_5b0b75dc_9190_4047_b9a8_14164cee8a31.YamlRule.Yaml)
    {
        for (int i = 0; i < 6; i++)
        {
            var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { WinEventExtensions.EventIdKey, "4625" },
                { WinEventExtensions.ChannelKey, "Security" },
                { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
                { WinEventExtensions.ProviderGuidKey, "54849625-5478-4994-A5BA-3E3B0328C30D" },
                { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
                { WinEventExtensions.ComputerKey, "LOCAL" }
            };

            var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "TargetUserName", "Auditor" },
                { "TargetDomainName", "CONTOSO" },
                { "IpAddress", "-" },
                { "LogonType", "2" },
                { "ProcessName", "C:\\\\Windows\\\\System32\\\\winlogon.exe" },
                { "LogonProcessName", "User32" },
                { "SubStatus", "0xc000006a" }
            };

            Add(new WinEvent(system, eventData));
        }

        Match = false;
    }
}