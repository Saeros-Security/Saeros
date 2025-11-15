using System.Linq.Expressions;
using Collector.Detection.Rules.Expressions.Predicates;
using Shared;

namespace Collector.Detection.Rules.Expressions;

internal sealed class NoOpBuildableExpressions : BuildableExpression
{
    private Expression<Func<WinEvent, bool>> Value { get; } = PredicateBuilder.New<WinEvent>(defaultExpression: false);

    public override Expression<Func<WinEvent, bool>> BuildPredicateExpression()
    {
        return Value;
    }
}