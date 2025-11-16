using Collector.Detection.Events.Details;
using Shared;

namespace Collector.Detection.Rules;

public readonly struct RuleMatch(bool match, DetectionDetails detectionDetails, TimeSpan duration, WinEvent winEvent)
{
    public bool Match { get; } = match;
    public DetectionDetails DetectionDetails { get; } = detectionDetails;
    public TimeSpan Duration { get; } = duration;
    public DateTimeOffset Date { get; } = winEvent.SystemTime.ToUniversalTime();
    public WinEvent WinEvent { get; } = winEvent;
}