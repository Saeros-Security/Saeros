using System.Text.RegularExpressions;
using Collector.Detection.Events.Lookups;
using Collector.Detection.Rules.Extensions;
using Shared;

namespace Collector.Detection.Rules.Builders.Executors;

internal static partial class Executor
{
    public static bool Grep(WinEvent winEvent, string expectedValue)
    {
        return winEvent.Contain(value => TryWildcard(expectedValue, value, RegexOptions.IgnoreCase, out var result) ? result : value.Contains(expectedValue, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Match(string key, IEnumerable<string> expectedValues, (string? value, bool abnormalPattern) actualValue, Func<ReadOnlySpan<char>, string?> aliasFactory, Func<ReadOnlySpan<char>, (string? value, bool abnormalPattern)> extractValue, Func<IEnumerable<string>> getAllValues, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        if (key.Contains(Constants.All, StringComparison.Ordinal))
        {
            return expectedValues.All(expectedValue => Match(key, expectedValue, actualValue, aliasFactory, extractValue, getAllValues, domainControllers, canProcessRegex, onRegexFailure));
        }
        
        return expectedValues.Any(expectedValue => Match(key, expectedValue, actualValue, aliasFactory, extractValue, getAllValues, domainControllers, canProcessRegex, onRegexFailure));
    }
    
    public static bool Match(string key, string expectedValue, (string? value, bool abnormalPattern) actualValue, Func<ReadOnlySpan<char>, string?> aliasFactory, Func<ReadOnlySpan<char>, (string? value, bool abnormalPattern)> extractValue, Func<IEnumerable<string>> getAllValues, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        if (expectedValue.Equals(Constants.Null, StringComparison.Ordinal) && actualValue.value == null) return true;
        if (TryWithPipe(key, expectedValue, aliasFactory, extractValue, getAllValues, domainControllers, canProcessRegex, onRegexFailure, out var result))
        {
            return result;
        }
        
        if (actualValue.value == null) return false;
        if (key.Equals(Constants.MinLength, StringComparison.Ordinal) && int.TryParse(expectedValue, out var minLength))
        {
            if (actualValue.abnormalPattern)
            {
                var allValues = actualValue.value.FromAbnormalPattern();
                return allValues.Any(value => value.Length >= minLength);
            }

            return actualValue.value.Length >= minLength;
        }
        
        if (TryWildcard(expectedValue, actualValue.value, RegexOptions.IgnoreCase, out result))
        {
            return result;
        }

        if (actualValue.abnormalPattern)
        {
            var values = actualValue.value.FromAbnormalPattern().ToHashSet(StringComparer.OrdinalIgnoreCase);
            return values.Contains(expectedValue);
        }

        return expectedValue.Equals(actualValue.value, StringComparison.OrdinalIgnoreCase);
    }
}