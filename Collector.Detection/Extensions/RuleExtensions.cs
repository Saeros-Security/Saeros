using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Collector.Detection.Events.Details;
using Collector.Detection.Rules;
using Collector.Detection.Rules.Predicates;
using Collector.Detection.Rules.Remote;
using FastExpressionCompiler;
using Remote.Linq;
using Remote.Linq.Text.Json;
using Shared;

namespace Collector.Detection.Extensions;

public static class RuleExtensions
{
    private static readonly JsonSerializerOptions JsonSerializerOptions;

    static RuleExtensions()
    {
        JsonSerializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            MaxDepth = 256
        }.ConfigureRemoteLinq();
    }

    public static void ToRemoteStream(this StandardRule rule, Stream stream)
    {
        var remoteRuleExpression = rule.RulePredicateExpression.ToRemoteLinqExpression();
        var remoteDetailsExpression = rule.DetailsPredicateExpression.ToRemoteLinqExpression();
        JsonSerializer.Serialize(stream, new StandardRemoteRule(rule.Metadata, remoteRuleExpression, remoteDetailsExpression), JsonSerializerOptions);
    }
    
    public static void ToRemoteStream(this AggregationRule rule, Stream stream)
    {
        var remoteRuleExpression = rule.RulePredicateExpression.ToRemoteLinqExpression();
        var remoteAggregateExpression = rule.AggregateExpression.ToRemoteLinqExpression();
        var remoteDetailsExpression = rule.DetailsPredicateExpression.ToRemoteLinqExpression();
        JsonSerializer.Serialize(stream, new AggregationRemoteRule(rule.Metadata, remoteRuleExpression, remoteAggregateExpression, remoteDetailsExpression, JsonSerializer.Serialize(rule.AggregationProperties)), JsonSerializerOptions);
    }

    public static RuleBase FromRemoteStream(this Stream stream, RuleType ruleType)
    {
        if (ruleType == RuleType.Standard)
        {
            return FromStandardRemoteRuleStream(stream);
        }
        
        if (ruleType == RuleType.Aggregation)
        {
            return FromAggregationRemoteRuleStream(stream);
        }
        
        throw new NotSupportedException($"Unsupported aggregation type: {ruleType}");
    }
    
    private static RuleBase FromStandardRemoteRuleStream(this Stream stream)
    {
        var remoteRule = DeserializeRemoteExpression<StandardRemoteRule>(stream);

        var ruleLinqExpression = ToLinqExpression<WinEvent, bool>(remoteRule!.RuleExpression!);
        var winEventPredicate = new WinEventPredicate(ruleLinqExpression.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), ruleLinqExpression);

        var detailsLinqExpression = ToLinqExpression<WinEvent, RuleMetadata, DetectionDetails>(remoteRule.DetailsExpression!);
        var detailsPredicate = new DetailsPredicate(detailsLinqExpression.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), detailsLinqExpression);
        return new StandardRule(remoteRule.RuleMetadata, winEventPredicate.Predicate, detailsPredicate.Predicate, winEventPredicate.PredicateExpression, detailsPredicate.PredicateExpression);
    }
    
    private static RuleBase FromAggregationRemoteRuleStream(this Stream stream)
    {
        var remoteRule = DeserializeRemoteExpression<AggregationRemoteRule>(stream);

        var ruleLinqExpression = ToLinqExpression<WinEvent, bool>(remoteRule!.RuleExpression!);
        var aggregationLinqExpression = ToLinqExpression<WinEvent?>(remoteRule.AggregateExpression!);
        var aggregationPredicate = new AggregationPredicate(ruleLinqExpression.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), aggregationLinqExpression.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), ruleLinqExpression, aggregationLinqExpression, JsonSerializer.Deserialize<ISet<string>>(remoteRule.JsonProperties) ?? new HashSet<string>());

        var detailsLinqExpression = ToLinqExpression<WinEvent, RuleMetadata, DetectionDetails>(remoteRule.DetailsExpression!);
        var detailsPredicate = new DetailsPredicate(detailsLinqExpression.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression), detailsLinqExpression);
        return new AggregationRule(remoteRule.RuleMetadata, aggregationPredicate.Predicate, aggregationPredicate.Aggregate, detailsPredicate.Predicate, aggregationPredicate.PredicateExpression, aggregationPredicate.AggregateExpression, detailsPredicate.PredicateExpression, aggregationPredicate.AggregationProperties);
    }

    private static T? DeserializeRemoteExpression<T>(Stream stream)
    {
        return JsonSerializer.Deserialize<T>(stream, JsonSerializerOptions);
    }

    private static Expression<Func<T>> ToLinqExpression<T>(Remote.Linq.Expressions.LambdaExpression expression)
    {
        var linqExpression = expression.ToLinqExpression();
        return Expression.Lambda<Func<T>>(linqExpression.Body, linqExpression.Parameters);
    }
    
    private static Expression<Func<T, TResult>> ToLinqExpression<T, TResult>(Remote.Linq.Expressions.LambdaExpression expression)
    {
        var linqExpression = expression.ToLinqExpression();
        return Expression.Lambda<Func<T, TResult>>(linqExpression.Body, linqExpression.Parameters);
    }

    private static Expression<Func<T1, T2, TResult>> ToLinqExpression<T1, T2, TResult>(Remote.Linq.Expressions.LambdaExpression expression)
    {
        var linqExpression = expression.ToLinqExpression();
        return Expression.Lambda<Func<T1, T2, TResult>>(linqExpression.Body, linqExpression.Parameters);
    }
}
