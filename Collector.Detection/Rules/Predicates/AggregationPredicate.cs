using System.Linq.Expressions;
using Shared;

namespace Collector.Detection.Rules.Predicates;

public record AggregationPredicate(Func<WinEvent, bool> Predicate, Func<WinEvent?> Aggregate, Expression<Func<WinEvent, bool>> PredicateExpression, Expression<Func<WinEvent?>> AggregateExpression, ISet<string> AggregationProperties);