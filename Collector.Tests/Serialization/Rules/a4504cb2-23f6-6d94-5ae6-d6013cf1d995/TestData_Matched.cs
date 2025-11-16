using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules.a4504cb2_23f6_6d94_5ae6_d6013cf1d995;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(a4504cb2_23f6_6d94_5ae6_d6013cf1d995.YamlRule.Yaml)
    {
        for (int i = 0; i < 11; i++)
        {
            var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { WinEventExtensions.EventIdKey, "4663" },
                { WinEventExtensions.ChannelKey, "Security" },
                { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
                { WinEventExtensions.ProviderGuidKey, "54849625-5478-4994-A5BA-3E3B0328C30D" },
                { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
                { WinEventExtensions.ComputerKey, "LOCAL" }
            };

            var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ObjectType", "File" },
                { "AccessList", "%%1537" },
                { "Keywords", "0x8020000000000000" },
                { "SubjectLogonId", "0x3e7" }
            };

            Add(new WinEvent(system, eventData));
        }

        Match = true;
        Details = "User: N/A \u00a6 ObjSvr: N/A \u00a6 ObjName: N/A \u00a6 ObjType: File \u00a6 Proc: N/A \u00a6 PID: N/A \u00a6 HID: N/A \u00a6 LID: 0x3e7";
    }
}