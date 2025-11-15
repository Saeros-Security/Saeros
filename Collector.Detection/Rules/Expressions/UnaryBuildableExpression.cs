namespace Collector.Detection.Rules.Expressions;

internal abstract class UnaryBuildableExpression(BuildableExpression inner) : BuildableExpression
{
    protected BuildableExpression Inner { get; } = inner;
}