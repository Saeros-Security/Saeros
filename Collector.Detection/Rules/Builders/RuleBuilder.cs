using System.Collections.Concurrent;
using System.Linq.Expressions;
using Collector.Detection.Contracts;
using Collector.Detection.Rules.Builders.Walkers;
using Collector.Detection.Rules.Detections;
using Collector.Detection.Rules.Expressions.Predicates;
using Collector.Detection.Rules.Extensions;
using Collector.Detection.Rules.Predicates;
using Detection.Yaml;
using FastExpressionCompiler;
using Shared;

namespace Collector.Detection.Rules.Builders;

internal static class RuleBuilder
{
    public static RuleBase Build(IList<YamlRule> yamlRules, RuleMetadata ruleMetadata, Aliases aliases, Details details, ChannelAbbrevations channelAbbreviations, ProviderAbbrevations providerAbbreviations, ISet<string> domainControllers, out ISet<ChannelEventId> channelEventIds, out ISet<ProviderEventId> providerEventIds, out ISet<string> properties)
    {
        var detailsPredicate = BuildDetailsPredicate();
        if (ruleMetadata.CorrelationOrAggregationTimeSpan.HasValue)
        {
            var aggregationPredicate = BuildAggregationPredicate(yamlRules, ruleMetadata, aliases, details, channelAbbreviations, providerAbbreviations, domainControllers, out channelEventIds, out providerEventIds, out properties);
            return new AggregationRule(ruleMetadata, aggregationPredicate.Predicate, aggregationPredicate.Aggregate, detailsPredicate.Predicate, aggregationPredicate.PredicateExpression, aggregationPredicate.AggregateExpression, detailsPredicate.PredicateExpression, aggregationPredicate.AggregationProperties);
        }
        else
        {
            var winEventPredicate = BuildWinEventPredicate(yamlRules, ruleMetadata, aliases, details, channelAbbreviations, providerAbbreviations, domainControllers, out channelEventIds, out providerEventIds, out properties);
            return new StandardRule(ruleMetadata, winEventPredicate.Predicate, detailsPredicate.Predicate, winEventPredicate.PredicateExpression, detailsPredicate.PredicateExpression);
        }
    }

    private static WinEventPredicate BuildWinEventPredicate(IList<YamlRule> yamlRules, RuleMetadata ruleMetadata, Aliases aliases, Details details, ChannelAbbrevations channelAbbreviations, ProviderAbbrevations providerAbbreviations, ISet<string> domainControllers, out ISet<ChannelEventId> channelEventIds, out ISet<ProviderEventId> providerEventIds, out ISet<string> properties)
    {
        var expression = yamlRules.BuildRuleExpression<Expression<Func<WinEvent, bool>>>(ruleMetadata, buildableExpression => buildableExpression.BuildPredicateExpression(), aliases, details, channelAbbreviations, providerAbbreviations, domainControllers, out channelEventIds, out providerEventIds, out properties);
        return new WinEventPredicate(expression.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), expression);
    }
    
    private static AggregationPredicate BuildAggregationPredicate(IList<YamlRule> yamlRules, RuleMetadata ruleMetadata, Aliases aliases, Details details, ChannelAbbrevations channelAbbreviations, ProviderAbbrevations providerAbbreviations, ISet<string> domainControllers, out ISet<ChannelEventId> channelEventIds, out ISet<ProviderEventId> providerEventIds, out ISet<string> properties)
    {
        var expressions = yamlRules.BuildRuleExpression<Tuple<Expression<Func<WinEvent, bool>>, Expression<Func<WinEvent?>>, ISet<string>>>(ruleMetadata, buildableExpression => buildableExpression.BuildAggregationExpression(), aliases, details, channelAbbreviations, providerAbbreviations, domainControllers, out channelEventIds, out providerEventIds, out properties);
        return new AggregationPredicate(expressions.Item1.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), expressions.Item2.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), expressions.Item1, expressions.Item2, expressions.Item3);
    }

    private static DetailsPredicate BuildDetailsPredicate()
    {
        var expression = YamlRuleExtensions.BuildDetailsExpression();
        return new DetailsPredicate(expression.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), expression);
    }
    
    public static IDictionary<string, DetectionExpressions> GetDetectionExpressionsByName(YamlRule yamlRule, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        return EnumerateDetections(yamlRule).Select(detection => new KeyValuePair<string, DetectionExpressions>(detection.Name, GetDetectionExpressions(detection, domainControllers, canProcessRegex, onRegexFailure))).ToDictionary(StringComparer.Ordinal);
    }
    
    private static IEnumerable<Detections.Detection> EnumerateDetections(YamlRule yamlRule)
    {
        foreach (var detection in yamlRule.Detections)
        {
            if (detection.Key.Equals(Constants.Condition)) continue;
            yield return Detections.Detection.Create(detection);
        }
    }

    private static DetectionExpressions GetDetectionExpressions(Detections.Detection detection, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        var expressionsByName = new ConcurrentDictionary<string, List<Expression<Func<WinEvent, bool>>>>(StringComparer.Ordinal);
        var expressions = ExtractExpression(Add, detection.Properties, domainControllers, canProcessRegex, onRegexFailure);
        return new DetectionExpressions(expressions, expressionsByName);
        
        void Add(Expression<Func<WinEvent, bool>> expression)
        {
            expressionsByName.AddOrUpdate(detection.Name, addValueFactory: _ => [expression], updateValueFactory: (_, current) =>
            {
                current.Add(expression);
                return current;
            });
        }
    }

    private static Expression<Func<WinEvent, bool>> ExtractExpression(Action<Expression<Func<WinEvent, bool>>> onExpressionBuilt, object properties, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        if (properties is IDictionary<string, object> dictionary)
        {
            return DictionaryWalker.Walk(onExpressionBuilt, dictionary, nested: false, domainControllers, canProcessRegex, onRegexFailure);
        }

        if (properties is IEnumerable<object> enumerable)
        {
            return EnumerableWalker.Walk(onExpressionBuilt, enumerable, nested: false, domainControllers, canProcessRegex, onRegexFailure);
        }

        return PredicateBuilder.New<WinEvent>(defaultExpression: false);
    }
}