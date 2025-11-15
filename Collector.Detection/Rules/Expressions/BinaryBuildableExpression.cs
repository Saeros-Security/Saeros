namespace Collector.Detection.Rules.Expressions;

internal abstract class BinaryBuildableExpression(BuildableExpression left, BuildableExpression right) : BuildableExpression
{
    protected BuildableExpression Left { get; } = left;
    protected BuildableExpression Right { get; } = right;
}