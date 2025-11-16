using System.Text.Json.Serialization;
using Remote.Linq.Expressions;

namespace Collector.Detection.Rules.Remote;

[method: JsonConstructor]
internal sealed class AggregationRemoteRule(RuleMetadata ruleMetadata, LambdaExpression? ruleExpression, LambdaExpression? aggregateExpression, LambdaExpression? detailsExpression, string jsonProperties)
{
    public RuleMetadata RuleMetadata { get; } = ruleMetadata;
    public LambdaExpression? RuleExpression { get; } = ruleExpression;
    public LambdaExpression? AggregateExpression { get; } = aggregateExpression;
    public LambdaExpression? DetailsExpression { get; } = detailsExpression;
    public string JsonProperties { get; } = jsonProperties;
}