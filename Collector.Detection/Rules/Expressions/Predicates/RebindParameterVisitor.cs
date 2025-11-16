using System.Linq.Expressions;

namespace Collector.Detection.Rules.Expressions.Predicates;

public class RebindParameterVisitor(
    ParameterExpression oldParameter,
    ParameterExpression newParameter)
    : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == oldParameter ? newParameter : base.VisitParameter(node);
    }
}