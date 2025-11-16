using Collector.Detection.Aggregations.Interfaces;
using Collector.Services.Abstractions.Rules;

namespace Collector.Services.Implementation.Agent.Rules;

public sealed class RulePropertiesProvider(IRuleService ruleService) : IProvideRuleProperties
{
    public ISet<string> GetProperties(string ruleId)
    {
        return ruleService.GetProperties(ruleId);
    }
}