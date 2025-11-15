using Collector.Detection.Rules;
using Streaming;

namespace Collector.Services.Implementation.Extensions;

public static class RuleMatchExtensions
{
    public static DetectionContract ToDetection(this RuleMatch ruleMatch)
    {
        return new DetectionContract
        {
            Details = ruleMatch.DetectionDetails.Details,
            Duration = ruleMatch.Duration.Ticks,
            Date = ruleMatch.Date.Ticks,
            Title = ruleMatch.DetectionDetails.RuleMetadata.Title,
            EventTitle = ruleMatch.DetectionDetails.EventTitle,
            JsonWinEvent = ruleMatch.WinEvent.ToJsonWinEvent(),
            Computer = ruleMatch.WinEvent.Computer,
            RuleId = ruleMatch.DetectionDetails.RuleMetadata.Id,
            Level = ruleMatch.DetectionDetails.RuleMetadata.Level
        };
    }

    public static RuleUpdateContract ToRuleUpdate(this RuleMatch ruleMatch)
    {
        return new RuleUpdateContract
        {
            Id = ruleMatch.DetectionDetails.RuleMetadata.Id,
            Updated = ruleMatch.Date.Ticks
        };
    }
}