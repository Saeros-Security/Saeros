using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Collector.Detection.Contracts;
using Collector.Detection.Events.Lookups;
using Collector.Detection.Rules.Builders.Executors;
using Collector.Detection.Rules.Expressions.Predicates;
using Collector.Detection.Rules.Extensions;
using Shared;

namespace Collector.Detection.Rules.Builders;

internal static class ExpressionBuilder
{
    private static readonly Regex BackslashRegex = new(@"\\\\+", RegexOptions.Compiled);

    public static Expression<Func<WinEvent, bool>> BuildGrepExpression(string value)
    {
        return PredicateBuilder.New<WinEvent>(winEvent => Executor.Grep(winEvent, value));
    }
    
    public static Expression<Func<WinEvent, bool>> BuildMatchExpression(string key, string value, string? parentNodeName, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        return PredicateBuilder.New<WinEvent>(winEvent => Match(winEvent, key, value, parentNodeName, domainControllers, canProcessRegex, onRegexFailure));
    }
    
    public static Expression<Func<WinEvent, bool>> BuildMatchExpression(string key, IList<string> values, string? parentNodeName, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        return PredicateBuilder.New<WinEvent>(winEvent => Match(winEvent, key, values, parentNodeName, domainControllers, canProcessRegex, onRegexFailure));
    }
    
    private static bool Match(WinEvent winEvent, string key, IList<string> values, string? parentNodeName, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        if (Aliases.Instance.Items.TryGetValue(key, out var alias))
        {
            key = alias;
        }
        
        return Executor.Match(key, values.Select(EscapeBackSlashes).ToList(), GetValue(key, parentNodeName, winEvent), aliasFactory: name => Aliases.Instance.Items.TryGetValue(name, out var found) ? found : null, fieldName => GetValue(fieldName, parentNodeName, winEvent), winEvent.EnumerateStringValues, domainControllers, canProcessRegex, onRegexFailure);
    }
    
    private static bool Match(WinEvent winEvent, string key, string value, string? parentNodeName, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        if (Aliases.Instance.Items.TryGetValue(key, out var alias))
        {
            key = alias;
        }
        
        return Executor.Match(key, EscapeBackSlashes(value), GetValue(key, parentNodeName, winEvent), aliasFactory: name => Aliases.Instance.Items.TryGetValue(name, out var found) ? found : null, fieldName => GetValue(fieldName, parentNodeName, winEvent), winEvent.EnumerateStringValues, domainControllers, canProcessRegex, onRegexFailure);
    }
    
    private static string EscapeBackSlashes(string value)
    {
        if (!value.Contains(@"\\", StringComparison.Ordinal)) return value;
        return BackslashRegex.Replace(value, m =>
        {
            var count = m.ValueSpan.Length / 2;
            return string.Concat(Enumerable.Repeat('\\', count));
        });
    }
    
    private static (string? Value, bool AbnormalPattern) GetValue(ReadOnlySpan<char> key, string? parentNodeName, WinEvent winEvent)
    {
        var abnormalPattern = false;
        var nestedKeywords = Constants.NestedKeywords.Contains(key);
        var input = nestedKeywords && !string.IsNullOrWhiteSpace(parentNodeName) ? parentNodeName : key;
        var attributesSpan = Constants.Attributes.AsSpan();
        if (input.Contains(attributesSpan, StringComparison.Ordinal))
        {
            var attributeName = input.SplitOnce(attributesSpan).Right;
            return TryGetAttributeValue(winEvent, attributeName, out var value) ? (value, abnormalPattern) : (value: null, abnormalPattern);
        }

        if (nestedKeywords)
        {
            if (input.Contains(Constants.DotString.AsSpan(), StringComparison.Ordinal))
            {
                return TryGetValue(winEvent, input, out var value) ? (value, abnormalPattern) : (value: null, abnormalPattern);
            }
            else
            {
                var value = winEvent.GetValue(input, out abnormalPattern);
                return (value, abnormalPattern);
            }
        }

        if (key.Contains(Constants.DotString.AsSpan(), StringComparison.Ordinal))
        {
            return TryGetValue(winEvent, key, out var value) ? (value, abnormalPattern) : (value: null, abnormalPattern);
        }
        else
        {
            var value = winEvent.GetValue(key, out abnormalPattern);
            return (value, abnormalPattern);   
        }
    }

    private static bool TryGetValue(WinEvent winEvent, ReadOnlySpan<char> input, [MaybeNullWhen(false)] out string value)
    {
        value = null;
        var systemLookup = ((Dictionary<string, string>)winEvent.System).GetAlternateLookup<ReadOnlySpan<char>>();
        var eventDataLookup = ((Dictionary<string, string>)winEvent.EventData).GetAlternateLookup<ReadOnlySpan<char>>();
        foreach (var range in input.Split(Constants.Dot))
        {
            var (offset, length) = range.GetOffsetAndLength(input.Length);
            var tail = offset + length == input.Length;
            if (!tail) continue;
            var leaf = input[range];
            if (systemLookup.TryGetValue(leaf, out var systemValue))
            {
                value = systemValue;
                return true;
            }
            
            if (eventDataLookup.TryGetValue(leaf, out var eventDataValue))
            {
                value = eventDataValue;
                return true;
            }
        }

        return false;
    }
    
    private static bool TryGetAttributeValue(WinEvent winEvent, ReadOnlySpan<char> attributeName, [MaybeNullWhen(false)] out string value)
    {
        value = null;
        if (winEvent.System.TryGetValue(attributeName, out var systemValue))
        {
            value = systemValue;
            return true;
        }
            
        if (winEvent.EventData.TryGetValue(attributeName, out var eventDataValue))
        {
            value = eventDataValue;
            return true;
        }

        return false;
    }
}