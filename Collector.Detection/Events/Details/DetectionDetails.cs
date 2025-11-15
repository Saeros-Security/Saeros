using Collector.Detection.Rules;

namespace Collector.Detection.Events.Details;

public readonly struct DetectionDetails(string eventTitle, string details, RuleMetadata ruleMetadata, DateTimeOffset timeStamp)
{
    public string EventTitle { get; } = eventTitle;
    public string Details { get; } = details;
    public RuleMetadata RuleMetadata { get; } = ruleMetadata;
    public DateTimeOffset TimeStamp { get; } = timeStamp;
}