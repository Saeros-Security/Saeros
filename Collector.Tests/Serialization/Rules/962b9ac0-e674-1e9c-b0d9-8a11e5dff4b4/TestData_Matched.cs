using Shared;
using Shared.Extensions;

namespace Collector.Tests.Serialization.Rules._962b9ac0_e674_1e9c_b0d9_8a11e5dff4b4;

public class TestData_Matched : TestData
{
    public TestData_Matched() : base(_962b9ac0_e674_1e9c_b0d9_8a11e5dff4b4.YamlRule.Yaml)
    {
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, "4738" },
            { WinEventExtensions.ChannelKey, "Security" },
            { WinEventExtensions.ProviderNameKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.ProviderGuidKey, "Microsoft-Windows-Security-Auditing" },
            { WinEventExtensions.SystemTimeKey, "2025-01-29T14:45:54.020972Z" },
            { WinEventExtensions.ComputerKey, "LOCAL" }
        };

        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "UserAccountControl", "%%2091" }
        };
            
        Add(new WinEvent(system, eventData));
        Match = true;
        Details = "User: N/A \u00a6 SID: N/A \u00a6 TgtUser: N/A \u00a6 TgtSID: N/A \u00a6 Domain: N/A \u00a6 TgtDomain: N/A \u00a6 SamUser: N/A \u00a6 DisplayName: N/A \u00a6 UAC: %%2091 \u00a6 OldUAC: N/A \u00a6 NewUAC: N/A \u00a6 AcctExpires: N/A \u00a6 AllowedToDelegateTo: N/A \u00a6 HomeDir: N/A \u00a6 HomePath: N/A \u00a6 LogonHours: N/A \u00a6  PwLastSet: N/A \u00a6 PrimaryGrpID: N/A \u00a6 PrivList: N/A \u00a6 ProfilePath: N/A \u00a6 ScriptPath: N/A \u00a6 SidHistory: N/A \u00a6 UserParams: N/A \u00a6 UPN: N/A \u00a6 SrcComp: N/A \u00a6 LID: N/A";
    }
}