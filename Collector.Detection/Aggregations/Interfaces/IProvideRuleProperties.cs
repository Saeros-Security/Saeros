namespace Collector.Detection.Aggregations.Interfaces;

public interface IProvideRuleProperties
{
    ISet<string> GetProperties(string ruleId);
}