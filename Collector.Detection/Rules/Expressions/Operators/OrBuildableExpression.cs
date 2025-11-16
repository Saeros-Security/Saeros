using System.Linq.Expressions;
using Collector.Detection.Rules.Expressions.Predicates;
using Shared;

namespace Collector.Detection.Rules.Expressions.Operators;

internal sealed class OrBuildableExpression(BuildableExpression left, BuildableExpression right) : BinaryBuildableExpression(left, right)
{
    public override Expression<Func<WinEvent, bool>> BuildPredicateExpression()
    {
        return Left.BuildPredicateExpression().Or(Right.BuildPredicateExpression());
    }
}