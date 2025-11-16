using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Collector.Detection.Contracts;
using Collector.Detection.Events.Details;
using Collector.Detection.Helpers;
using Collector.Detection.Rules.Builders;
using Collector.Detection.Rules.Detections;
using Collector.Detection.Rules.Expressions;
using Collector.Detection.Rules.Parsers;
using Detection.Yaml;
using Shared;
using Constants = Collector.Detection.Rules.Builders.Constants;

namespace Collector.Detection.Rules.Extensions;

public static class YamlRuleExtensions
{
    private static readonly Regex DetailRegex = new("(?<=%)(.*?)(?=%)", RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, byte> FailingRegexes = new();

    public static T BuildRuleExpression<T>(this IList<YamlRule> yamlRules, RuleMetadata ruleMetadata, Func<BuildableExpression, T> build, Aliases aliases, Details details, ChannelAbbrevations channelAbbreviations, ProviderAbbrevations providerAbbreviations, ISet<string> domainControllers, out ISet<ChannelEventId> channelEventIds, out ISet<ProviderEventId> providerEventIds, out ISet<string> properties)
    {
        var detectionExpressionsByRuleId = new Dictionary<string, IDictionary<string, DetectionExpressions>>(StringComparer.Ordinal);
        foreach (var yamlRule in yamlRules)
        {
            detectionExpressionsByRuleId[yamlRule.Id] = RuleBuilder.GetDetectionExpressionsByName(yamlRule, domainControllers, canProcessRegex: pattern => !FailingRegexes.ContainsKey(pattern), onRegexFailure: pattern => FailingRegexes.TryAdd(pattern, 1));
        }

        channelEventIds = EnumerateChannelEventIds(yamlRules, channelAbbreviations);
        providerEventIds = EnumerateProviderEventIds(yamlRules, providerAbbreviations);
        EnrichProviders(channelEventIds, providerEventIds);
        properties = EnumerateProperties(yamlRules, aliases);
        EnrichProperties(ruleMetadata.Details, details, providerEventIds, properties, aliases);
        properties = SanitizeProperties(properties);
        var ruleParser = new RuleParser(yamlRules, detectionExpressionsByRuleId);
        var buildableExpression = ruleParser.Parse(ruleMetadata);
        return build(buildableExpression);
    }
    
    private static ISet<string> SanitizeProperties(ISet<string> properties)
    {
        var sanitizedProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            if (!property.Contains(Constants.Dot))
            {
                sanitizedProperties.Add($"Event.EventData.{property.Replace(Constants.Attributes, string.Empty, StringComparison.OrdinalIgnoreCase).Trim()}");
            }
            else
            {
                sanitizedProperties.Add(property.Replace(Constants.Attributes, string.Empty, StringComparison.OrdinalIgnoreCase).Trim());
            }
        }

        return sanitizedProperties;
    }
    
    private static void ExtractPropertiesFromDetails(string details, Aliases aliases, ISet<string> properties)
    {
        var parts = details.Split(Constants.DetailSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (DetailRegex.IsMatch(part))
            {
                var match = DetailRegex.Match(part).Value;
                if (aliases.Items.TryGetValue(match, out var alias))
                {
                    properties.Add(alias);
                }
                else
                {
                    properties.Add(match);
                }
            }
        }
    }

    private static void EnrichProperties(string? ruleDetails, Details details, ISet<ProviderEventId> providerEventIds, ISet<string> properties, Aliases aliases)
    {
        foreach (var providerEventId in providerEventIds)
        {
            if (details.Items.TryGetValue(providerEventId, out var detail))
            {
                ExtractPropertiesFromDetails(detail, aliases, properties);
            }
        }
        
        ExtractPropertiesFromDetails(ruleDetails ?? string.Empty, aliases, properties);
    }
    
    private static ISet<string> EnumerateProperties(IList<YamlRule> yamlRules, Aliases aliases)
    {
        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var yamlRule in yamlRules)
        {
            foreach (var detection in yamlRule.Detections)
            {
                if (detection.Key.Equals(Constants.Condition)) continue;
                if (detection.Value is IDictionary<string, object> conditions)
                {
                    foreach (var property in conditions.EnumerateKeys(aliases.Items))
                    {
                        properties.Add(property);
                    }
                }
            }
            
            foreach (var detection in yamlRule.Correlations)
            {
                if (!detection.Key.Equals(Constants.GroupBy)) continue;
                if (detection.Value is IEnumerable<object> values)
                {
                    foreach (var property in values.OfType<string>())
                    {
                        properties.Add(property);
                    }
                }
            }
        }

        return properties;
    }
    
    private static void EnrichProviders(ISet<ChannelEventId> channelEventIds, ISet<ProviderEventId> providerEventIds)
    {
        foreach (var channelEventId in channelEventIds)
        {
            if (channelEventId.Channel.Contains(Constants.Slash))
            {
                var parts = channelEventId.Channel.Split(Constants.Slash, StringSplitOptions.RemoveEmptyEntries);
                providerEventIds.Add(new ProviderEventId(parts[0], channelEventId.EventId));
            }
        }
    }
    
    private static ISet<ChannelEventId> EnumerateChannelEventIds(IList<YamlRule> yamlRules, ChannelAbbrevations channelAbbreviations)
    {
        var channelEventIds = new HashSet<ChannelEventId>();
        foreach (var yamlRule in yamlRules)
        {
            foreach (var detection in yamlRule.Detections)
            {
                if (detection.Key.Equals(Constants.Condition)) continue;
                if (detection.Value is IDictionary<string, object> innerDetection)
                {
                    if (innerDetection.ContainsKey(Constants.Provider)) continue;
                    if (innerDetection.TryGetValue(Constants.Channel, out var channel) && innerDetection.TryGetValue(Constants.EventId, out var eventId))
                    {
                        if (channel is string channelName)
                        {
                            if (channelAbbreviations.Items.TryGetValue(channelName, out var fromAbbreviation))
                            {
                                foreach (var id in EnumerateEventIds(eventId))
                                {
                                    channelEventIds.Add(new ChannelEventId(fromAbbreviation, id));
                                }
                            }
                            else
                            {
                                foreach (var id in EnumerateEventIds(eventId))
                                {
                                    channelEventIds.Add(new ChannelEventId(channelName, id));
                                }
                            }
                        }
                        else if (channel is IEnumerable<object> channels)
                        {
                            foreach (var name in channels.OfType<string>())
                            {
                                if (channelAbbreviations.Items.TryGetValue(name, out var fromAbbreviation))
                                {
                                    foreach (var id in EnumerateEventIds(eventId))
                                    {
                                        channelEventIds.Add(new ChannelEventId(fromAbbreviation, id));
                                    }
                                }
                                else
                                {
                                    foreach (var id in EnumerateEventIds(eventId))
                                    {
                                        channelEventIds.Add(new ChannelEventId(name, id));
                                    }
                                }
                            }
                        }
                    }
                    else if (innerDetection.TryGetValue(Constants.Channel, out channel) && !innerDetection.TryGetValue(Constants.EventId, out eventId))
                    {
                        if (channel is string channelName)
                        {
                            if (channelAbbreviations.Items.TryGetValue(channelName, out var fromAbbreviation))
                            {
                                channelEventIds.Add(new ChannelEventId(fromAbbreviation, eventId: null));
                            }
                            else
                            {
                                channelEventIds.Add(new ChannelEventId(channelName, eventId: null));
                            }
                        }
                        else if (channel is IEnumerable<object> channels)
                        {
                            foreach (var name in channels.OfType<string>())
                            {
                                if (channelAbbreviations.Items.TryGetValue(name, out var fromAbbreviation))
                                {
                                    channelEventIds.Add(new ChannelEventId(fromAbbreviation, eventId: null));
                                }
                                else
                                {
                                    channelEventIds.Add(new ChannelEventId(name, eventId: null));
                                }
                            }
                        }
                    }
                    else if (!innerDetection.TryGetValue(Constants.Channel, out channel) && innerDetection.TryGetValue(Constants.EventId, out eventId))
                    {
                        foreach (var id in EnumerateEventIds(eventId))
                        {
                            channelEventIds.Add(new ChannelEventId(channel: null, id));
                        }
                    }
                }
            }
        }

        return channelEventIds;
    }

    private static IEnumerable<string> EnumerateEventIds(object eventId)
    {
        if (eventId is IEnumerable<object> eventIds)
        {
            foreach (var id in eventIds.OfType<string>())
            {
                yield return id;
            }
        }
        else if (eventId is string value)
        {
            yield return value;
        }
    }

    private static ISet<ProviderEventId> EnumerateProviderEventIds(IList<YamlRule> yamlRules, ProviderAbbrevations providerAbbreviations)
    {
        var providerEventIds = new HashSet<ProviderEventId>();
        foreach (var yamlRule in yamlRules)
        {
            foreach (var detection in yamlRule.Detections)
            {
                if (detection.Key.Equals(Constants.Condition)) continue;
                if (detection.Value is IDictionary<string, object> innerDetection)
                {
                    if (innerDetection.ContainsKey(Constants.Channel)) continue;
                    if (innerDetection.TryGetValue(Constants.Provider, out var provider) && innerDetection.TryGetValue(Constants.EventId, out var eventId))
                    {
                        if (provider is string providerName)
                        {
                            if (providerAbbreviations.Items.TryGetValue(providerName, out var fromAbbreviation))
                            {
                                foreach (var id in EnumerateEventIds(eventId))
                                {
                                    providerEventIds.Add(new ProviderEventId(fromAbbreviation, id));
                                }
                            }
                            else
                            {
                                foreach (var id in EnumerateEventIds(eventId))
                                {
                                    providerEventIds.Add(new ProviderEventId(providerName, id));
                                }
                            }
                        }
                        else if (provider is IEnumerable<object> providers)
                        {
                            foreach (var name in providers.OfType<string>())
                            {
                                if (providerAbbreviations.Items.TryGetValue(name, out var fromAbbreviation))
                                {
                                    foreach (var id in EnumerateEventIds(eventId))
                                    {
                                        providerEventIds.Add(new ProviderEventId(fromAbbreviation, id));
                                    }
                                }
                                else
                                {
                                    foreach (var id in EnumerateEventIds(eventId))
                                    {
                                        providerEventIds.Add(new ProviderEventId(name, id));
                                    }
                                }
                            }
                        }
                    }
                    else if (innerDetection.TryGetValue(Constants.Provider, out provider) && !innerDetection.TryGetValue(Constants.EventId, out eventId))
                    {
                        if (provider is string providerName)
                        {
                            if (providerAbbreviations.Items.TryGetValue(providerName, out var fromAbbreviation))
                            {
                                providerEventIds.Add(new ProviderEventId(fromAbbreviation, eventId: null));
                            }
                            else
                            {
                                providerEventIds.Add(new ProviderEventId(providerName, eventId: null));
                            }
                        }
                        else if (provider is IEnumerable<object> providers)
                        {
                            foreach (var name in providers.OfType<string>())
                            {
                                if (providerAbbreviations.Items.TryGetValue(name, out var fromAbbreviation))
                                {
                                    providerEventIds.Add(new ProviderEventId(fromAbbreviation, eventId: null));
                                }
                                else
                                {
                                    providerEventIds.Add(new ProviderEventId(name, eventId: null));
                                }
                            }
                        }
                    }
                    else if (!innerDetection.TryGetValue(Constants.Provider, out provider) && innerDetection.TryGetValue(Constants.EventId, out eventId))
                    {
                        foreach (var id in EnumerateEventIds(eventId))
                        {
                            providerEventIds.Add(new ProviderEventId(provider: null, id));
                        }
                    }
                }
            }
        }

        return providerEventIds;
    }
    
    public static Expression<Func<WinEvent, RuleMetadata, DetectionDetails>> BuildDetailsExpression()
    {
        return (winEvent, ruleDetails) => DetectionDetailsResolver.Resolve(winEvent, ruleDetails);
    }

    public static RuleMetadata ToMetadata(this IList<YamlRule> yamlRules)
    {
        var correlationRule = yamlRules.SingleOrDefault(rule => rule.IsCorrelation());
        if (correlationRule is not null)
        {
            if (string.IsNullOrEmpty(correlationRule.Description))
            {
                var otherRule = yamlRules.Where(rule => !rule.IsCorrelation()).Aggregate((left, right) => !string.IsNullOrEmpty(left.Description) ? left : right);
                return new RuleMetadata(correlationRule.Id, SigmaRuleMetadataHelper.OverrideTitle(otherRule.Id, otherRule.Title), otherRule.Date, otherRule.Modified, otherRule.Author, otherRule.Details, SigmaRuleMetadataHelper.OverrideDescription(otherRule.Id, otherRule.Description), otherRule.Level, otherRule.Status, otherRule.Tags, otherRule.References, otherRule.FalsePositives, correlationOrAggregationTimeSpan: correlationRule.TryGetTimeframe(out var correlationTimeSpan) ? correlationTimeSpan : null);
            }
            else
            {
                return new RuleMetadata(correlationRule.Id, SigmaRuleMetadataHelper.OverrideTitle(correlationRule.Id, correlationRule.Title), correlationRule.Date, correlationRule.Modified, correlationRule.Author, correlationRule.Details, SigmaRuleMetadataHelper.OverrideDescription(correlationRule.Id, correlationRule.Description), correlationRule.Level, correlationRule.Status, correlationRule.Tags, correlationRule.References, correlationRule.FalsePositives, correlationOrAggregationTimeSpan: correlationRule.TryGetTimeframe(out var correlationTimeSpan) ? correlationTimeSpan : null);
            }
        }

        var rule = yamlRules.Single();
        return new RuleMetadata(rule.Id, SigmaRuleMetadataHelper.OverrideTitle(rule.Id, rule.Title), rule.Date, rule.Modified, rule.Author, rule.Details, SigmaRuleMetadataHelper.OverrideDescription(rule.Id, rule.Description), rule.Level, rule.Status, rule.Tags, rule.References, rule.FalsePositives, correlationOrAggregationTimeSpan: rule.TryGetTimeframe(out var aggregationTimeSpan) ? aggregationTimeSpan : null);
    }

    public static RuleMetadata ToMetadata(this YamlRule yamlRule)
    {
        if (yamlRule.IsCorrelation())
        {
            return new RuleMetadata(yamlRule.Id, SigmaRuleMetadataHelper.OverrideTitle(yamlRule.Id, yamlRule.Title), yamlRule.Date, yamlRule.Modified, yamlRule.Author, yamlRule.Details, SigmaRuleMetadataHelper.OverrideDescription(yamlRule.Id, yamlRule.Description), yamlRule.Level, yamlRule.Status, yamlRule.Tags, yamlRule.References, yamlRule.FalsePositives, correlationOrAggregationTimeSpan: yamlRule.TryGetTimeframe(out var correlationTimeSpan) ? correlationTimeSpan : null);
        }

        return new RuleMetadata(yamlRule.Id, SigmaRuleMetadataHelper.OverrideTitle(yamlRule.Id, yamlRule.Title), yamlRule.Date, yamlRule.Modified, yamlRule.Author, yamlRule.Details, SigmaRuleMetadataHelper.OverrideDescription(yamlRule.Id, yamlRule.Description), yamlRule.Level, yamlRule.Status, yamlRule.Tags, yamlRule.References, yamlRule.FalsePositives, correlationOrAggregationTimeSpan: yamlRule.TryGetTimeframe(out var aggregationTimeSpan) ? aggregationTimeSpan : null);
    }

    private static bool TryGetTimeframe(this YamlRule yamlRule, out TimeSpan timeframe)
    {
        timeframe = Events.Constants.DefaultTimeFrame;
        if (yamlRule.TryGetFromDetection<string>(Constants.Timeframe, out var value))
        {
            timeframe = value.ToTimeframe();
            return true;
        }

        if (yamlRule.Detections.Any(pair => pair.Value is string detection && detection.Contains('|', StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        
        if (yamlRule.TryGetFromCorrelation<string>(Constants.Timespan, out value))
        {
            timeframe = value.ToTimeframe();
            return true;
        }

        return false;
    }
    
    public static bool TryGetConditionNode(this YamlRule yamlRule, [MaybeNullWhen(false)] out string conditionNode)
    {
        conditionNode = null;
        if (!yamlRule.Detections.TryGetValue(Constants.Condition, out var condition))
        {
            return false;
        }

        conditionNode = condition as string;
        return !string.IsNullOrEmpty(conditionNode);
    }
    
    public static string? GetCorrelationProperty(this YamlRule correlationRule)
    {
        if (correlationRule.TryGetFromCorrelation<IDictionary<string, object>>(Constants.Condition, out var condition))
        {
            if (condition.TryGetValue(Constants.Field, out var field) && field is string fieldString)
            {
                return fieldString;
            }
        }

        return null;
    }
    
    public static string[] GetCorrelationDimensions(this YamlRule correlationRule)
    {
        if (correlationRule.TryGetFromCorrelation<string>(Constants.GroupBy, out var groupBy))
        {
            return [groupBy];
        }
        
        if (correlationRule.TryGetFromCorrelation<IEnumerable<string>>(Constants.GroupBy, out var groupByEnumerable))
        {
            return groupByEnumerable.ToArray();
        }

        return [];
    }
    
    public static string[] GetCorrelationRules(this YamlRule correlationRule)
    {
        if (correlationRule.TryGetFromCorrelation<string>(Constants.Rules, out var rules))
        {
            return [rules];
        }
        
        if (correlationRule.TryGetFromCorrelation<IEnumerable<string>>(Constants.Rules, out var rulesEnumerable))
        {
            return rulesEnumerable.ToArray();
        }

        return [];
    }

    public static string GetCorrelationOperator(this YamlRule correlationRule, out string value)
    {
        value = string.Empty;
        if (correlationRule.TryGetFromCorrelation<IDictionary<string, object>>(Constants.Condition, out var condition))
        {
            if (condition.TryGetValue(Constants.Gte, out var gte) && gte is string gteString)
            {
                value = gteString;
                return Constants.Gte;
            }
            
            if (condition.TryGetValue(Constants.Gt, out var gt) && gt is string gtString)
            {
                value = gtString;
                return Constants.Gt;
            }
            
            if (condition.TryGetValue(Constants.Lte, out var lte) && lte is string lteString)
            {
                value = lteString;
                return Constants.Lte;
            }
            
            if (condition.TryGetValue(Constants.Lt, out var lt) && lt is string ltString)
            {
                value = ltString;
                return Constants.Lt;
            }
        }

        return string.Empty;
    }
    
    private static bool TryGetFromDetection<T>(this YamlRule yamlRule, string item, [MaybeNullWhen(false)] out T result) where T : notnull
    {
        result = default;
        if (yamlRule.Detections.TryGetValue(item, out var value) && value is T finalResult)
        {
            result = finalResult;
            return true;
        }

        return false;
    }
    
    private static bool TryGetFromCorrelation<T>(this YamlRule yamlRule, string item, [MaybeNullWhen(false)] out T result) where T : notnull
    {
        result = default;
        if (yamlRule.Correlations.TryGetValue(item, out var value) && value is T finalResult)
        {
            result = finalResult;
            return true;
        }

        return false;
    }
    
    public static bool IsCorrelation(this YamlRule yamlRule) => yamlRule.Correlations.Count != 0;
}