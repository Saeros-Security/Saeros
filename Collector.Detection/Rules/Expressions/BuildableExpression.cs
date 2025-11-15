using System.Linq.Expressions;
using Shared;

namespace Collector.Detection.Rules.Expressions;

public class BuildableExpression
{
    public virtual Expression<Func<WinEvent, bool>> BuildPredicateExpression()
    {
        throw new NotImplementedException();
    }

    public virtual Tuple<Expression<Func<WinEvent, bool>>, Expression<Func<WinEvent?>>, ISet<string>> BuildAggregationExpression()
    {
        throw new NotImplementedException();
    }
}