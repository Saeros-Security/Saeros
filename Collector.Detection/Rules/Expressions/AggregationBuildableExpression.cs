using System.Linq.Expressions;
using Shared;

namespace Collector.Detection.Rules.Expressions;

internal class AggregationBuildableExpression(Expression<Func<WinEvent, bool>> predicate, Expression<Func<WinEvent?>> aggregate, ISet<string> aggregationProperties) : BuildableExpression
{
    public override Tuple<Expression<Func<WinEvent, bool>>, Expression<Func<WinEvent?>>, ISet<string>> BuildAggregationExpression()
    {
        return new Tuple<Expression<Func<WinEvent, bool>>, Expression<Func<WinEvent?>>, ISet<string>>(predicate, aggregate, aggregationProperties);
    }
}