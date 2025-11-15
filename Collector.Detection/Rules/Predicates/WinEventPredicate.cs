using System.Linq.Expressions;
using Shared;

namespace Collector.Detection.Rules.Predicates;

public record WinEventPredicate(Func<WinEvent, bool> Predicate, Expression<Func<WinEvent, bool>> PredicateExpression);