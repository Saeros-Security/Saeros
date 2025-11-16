using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._6683ccd7_da7a_b988_1683_7f7a1bf72bf6;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(_6683ccd7_da7a_b988_1683_7f7a1bf72bf6.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "4674" },
            { WinEventExtensions.ChannelKey, "Security" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.ProviderGuidKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ObjectName", "\\Device\\ConDrv" },
            { "ObjectServer", "Security" },
            { "ObjectType", "File" },
            { "SubjectLogonId", "0x2608c7" },
            { "SubjectUserName", "bbougot" },
            { "AccessMask", "%%1537 %%1538 %%1539 %%1540 %%1541 %%4416 %%4417 %%4418 %%4419 %%4420 %%4421 %%4422 %%4423 %%4424" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "Svc: \\Device\\ConDrv ¦ User: bbougot ¦ AccessMask: DELETE READ_CONTROL WRITE_DAC WRITE_OWNER SYNCHRONIZE READ_DATA WRITE_DATA APPEND_DATA READ_EXTENDED_ATTRIBUTES WRITE_EXTENDED_ATTRIBUTES EXECUTE/TRAVERSE DELETE_CHILD READ_ATTRIBUTES WRITE_ATTRIBUTES ¦ LID: 0x2608c7";
    }
}