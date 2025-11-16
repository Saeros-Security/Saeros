using System.Linq.Expressions;
using Collector.Detection.Events.Details;
using Shared;

namespace Collector.Detection.Rules.Predicates;

public record DetailsPredicate(Func<WinEvent, RuleMetadata, DetectionDetails> Predicate, Expression<Func<WinEvent, RuleMetadata, DetectionDetails>> PredicateExpression);