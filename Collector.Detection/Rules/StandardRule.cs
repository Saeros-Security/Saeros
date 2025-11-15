using System.Diagnostics;
using System.Linq.Expressions;
using Collector.Detection.Events.Details;
using Shared;

namespace Collector.Detection.Rules;

public sealed class StandardRule(RuleMetadata metadata, Func<WinEvent, bool> rulePredicate, Func<WinEvent, RuleMetadata, DetectionDetails> detailsPredicate, Expression<Func<WinEvent, bool>> rulePredicateExpression, Expression<Func<WinEvent, RuleMetadata, DetectionDetails>> detailsPredicateExpression)
    : RuleBase(metadata)
{
    public bool TryMatch(WinEvent winEvent, out RuleMatch ruleMatch)
    {
        ruleMatch = new RuleMatch();
        var watch = Stopwatch.StartNew();
        var match = rulePredicate(winEvent);
        watch.Stop();
        if (!match) return false;
        ruleMatch = new RuleMatch(match, detailsPredicate(winEvent, Metadata), watch.Elapsed, winEvent);
        return true;
    }
    
    public Expression<Func<WinEvent, bool>> RulePredicateExpression { get; } = rulePredicateExpression;
    public Expression<Func<WinEvent, RuleMetadata, DetectionDetails>> DetailsPredicateExpression { get; } = detailsPredicateExpression;
}