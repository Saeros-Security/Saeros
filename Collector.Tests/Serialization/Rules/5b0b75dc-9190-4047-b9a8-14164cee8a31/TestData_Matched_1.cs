using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._5b0b75dc_9190_4047_b9a8_14164cee8a31;

public class TestData_Matched_1 : TestData
{
    public TestData_Matched_1() : base(_5b0b75dc_9190_4047_b9a8_14164cee8a31.YamlRule.Yaml)
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
                { "IpAddress", "127.0.0.1" },
                { "LogonType", "2" },
                { "ProcessName", "C:\\\\Windows\\\\System32\\\\winlogon.exe" },
                { "LogonProcessName", "User32" },
                { "SubStatus", "0xc000006a" }
            };
            
            Add(new WinEvent(system, eventData));
        }
        
        Match = true;
        Details = "Type: 2 - INTERACTIVE \u00a6 TgtUser: Auditor \u00a6 SrcComp: N/A \u00a6 SrcIP: 127.0.0.1 \u00a6 AuthPkg: N/A \u00a6 Proc: C:\\\\Windows\\\\System32\\\\winlogon.exe";
    }
}