using System.Diagnostics;
using System.Linq.Expressions;
using Collector.Detection.Events.Details;
using Shared;
using Constants = Collector.Detection.Events.Constants;

namespace Collector.Detection.Rules;

public sealed class AggregationRule(RuleMetadata metadata, Func<WinEvent, bool> rulePredicate, Func<WinEvent?> aggregate, Func<WinEvent, RuleMetadata, DetectionDetails> detailsPredicate, Expression<Func<WinEvent, bool>> rulePredicateExpression, Expression<Func<WinEvent?>> aggregateExpression, Expression<Func<WinEvent, RuleMetadata, DetectionDetails>> detailsPredicateExpression, ISet<string> aggregationProperties)
    : RuleBase(metadata)
{
    public bool TryMatch(out RuleMatch ruleMatch)
    {
        ruleMatch = new RuleMatch();
        var watch = Stopwatch.StartNew();
        var winEvent = aggregate();
        watch.Stop();
        if (winEvent == null) return false;
        ruleMatch = new RuleMatch(true, detailsPredicate(winEvent, Metadata), watch.Elapsed, winEvent);
        return true;
    }

    public bool TryMatch(WinEvent winEvent)
    {
        return rulePredicate(winEvent);
    }

    public ISet<string> AggregationProperties => aggregationProperties;
    public TimeSpan CorrelationOrAggregationTimeSpan => Metadata.CorrelationOrAggregationTimeSpan ?? Constants.DefaultTimeFrame;
    
    public Expression<Func<WinEvent, bool>> RulePredicateExpression { get; } = rulePredicateExpression;
    public Expression<Func<WinEvent?>> AggregateExpression { get; } = aggregateExpression;
    public Expression<Func<WinEvent, RuleMetadata, DetectionDetails>> DetailsPredicateExpression { get; } = detailsPredicateExpression;
}