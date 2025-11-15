using System.Linq.Expressions;
using Collector.Detection.Rules.Expressions.Predicates;
using Shared;

namespace Collector.Detection.Rules.Expressions.Operators;

internal sealed class NegateBuildableExpression(BuildableExpression inner) : UnaryBuildableExpression(inner)
{
    public override Expression<Func<WinEvent, bool>> BuildPredicateExpression()
    {
        return Inner.BuildPredicateExpression().Not();
    }
}