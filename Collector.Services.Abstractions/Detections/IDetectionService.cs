using Collector.Detection.Rules;

namespace Collector.Services.Abstractions.Detections;

public interface IDetectionService
{
    void Send(RuleMatch ruleMatch);
}