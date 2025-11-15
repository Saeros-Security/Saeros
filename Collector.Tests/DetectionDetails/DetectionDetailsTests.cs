using Collector.Detection.Events.Details;
using FluentAssertions;
using Shared.Extensions;

namespace Collector.Tests.DetectionDetails;

public class DetectionDetailsTests
{
    [Fact]
    public void DetectionDetailsResolver_Should_Resolve()
    {
        var winEvent = DetectionDetailsResolver.Resolve(provider: "Microsoft-Windows-Security-Auditing", providerGuid: "54849625-5478-4994-a5ba-3e3b0328c30d", channel: "Security", systemTime: "2025-02-08T13:36:49.6031961+00:00", computer: "BENJAMINBOU8814.pathways.company", eventId: "4673", ruleDetails: "Proc: %ProcessName% \u00a6 User: %SubjectUserName% \u00a6 LID: %SubjectLogonId%", details: "Proc: C:\\Users\\bbougot\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe \u00a6 User: bbougot \u00a6 LID: 0x86ccf");
        winEvent.System.Should().ContainKey(WinEventExtensions.ProviderGuidKey);
        winEvent.System.Should().ContainKey(WinEventExtensions.ProviderNameKey);
        winEvent.System.Should().ContainKey(WinEventExtensions.ChannelKey);
        winEvent.System.Should().ContainKey(WinEventExtensions.ComputerKey);
        winEvent.System.Should().ContainKey(WinEventExtensions.SystemTimeKey);
        winEvent.System.Should().ContainKey(WinEventExtensions.EventIdKey);
        
        winEvent.EventData.Should().ContainKey("ProcessName");
        winEvent.EventData.Should().ContainKey("SubjectLogonId");
        winEvent.EventData.Should().ContainKey("SubjectUserName");
    }
}