using Collector.Core.Hubs.Detections;
using Collector.Core.Hubs.Rules;
using Collector.Detection.Extensions;
using Collector.Detection.Rules;
using Collector.Services.Abstractions.Detections;
using Collector.Services.Implementation.Extensions;

namespace Collector.Services.Implementation.Agent.Detections;

public sealed class DetectionServiceAgent(IStreamingDetectionHub streamingDetectionHub, IStreamingRuleHub streamingRuleHub) : IDetectionService
{
    public void Send(RuleMatch ruleMatch)
    {
        if (ruleMatch.Filter()) return;
        streamingDetectionHub.SendDetection(ruleMatch.ToDetection());
        streamingRuleHub.SendRuleUpdate(ruleMatch.ToRuleUpdate());
    }
}