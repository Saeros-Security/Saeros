using System.Text.Json.Serialization;
using Remote.Linq.Expressions;

namespace Collector.Detection.Rules.Remote;

internal sealed class StandardRemoteRule
{
    [JsonConstructor]
    public StandardRemoteRule(RuleMetadata ruleMetadata, LambdaExpression? ruleExpression, LambdaExpression? detailsExpression)
    {
        RuleMetadata = ruleMetadata;
        RuleExpression = ruleExpression;
        DetailsExpression = detailsExpression;
    }

    public RuleMetadata RuleMetadata { get; }
    public LambdaExpression? RuleExpression { get; }
    public LambdaExpression? DetailsExpression { get; }
}