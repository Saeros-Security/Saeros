using System.Net;
using System.Text.RegularExpressions;
using Collector.Detection.Rules.Builders.Executors.Helpers;
using Collector.Detection.Rules.Extensions;
using Shared.Extensions;

namespace Collector.Detection.Rules.Builders.Executors;

internal static partial class Executor
{
    private static bool TryWithPipe(string key, string expectedValue, Func<ReadOnlySpan<char>, string?> aliasFactory, Func<ReadOnlySpan<char>, (string? value, bool abnormalPattern)> extractValue, Func<IEnumerable<string>> getAllValues, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure, out bool result)
    {
        result = false;
        if (key.Contains(Constants.Pipe, StringComparison.Ordinal))
        {
            result = Pipe(key, expectedValue, aliasFactory, extractValue, getAllValues, domainControllers, canProcessRegex, onRegexFailure);
            return true;
        }

        return false;
    }
    
    private static bool Pipe(string key, string expectedValue, Func<ReadOnlySpan<char>, string?> aliasFactory, Func<ReadOnlySpan<char>, (string? value, bool abnormalPattern)> extractValue, Func<IEnumerable<string>> getAllValues, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure)
    {
        if (All(key, expectedValue, getAllValues, out var result))
        {
            return result;
        }

        var split = key.SplitOnce(separator: Constants.PipeString.AsSpan());
        var expected = split.Left;
        var modifier = split.Right;
        var alias = aliasFactory(expected);
        if (alias is not null)
        {
            expected = alias;
        }

        var extractedValue = extractValue(expected);
        foreach (var value in GetValues(extractedValue))
        {
            if (EndsWith(modifier, expectedValue, value, out result))
            {
                return result;
            }
            
            if (Contains(modifier, expectedValue, value, out result))
            {
                return result;
            }
            
            if (StartsWith(modifier, expectedValue, value, out result))
            {
                return result;
            }
            
            if (Cidr(modifier, expectedValue, value, out result))
            {
                return result;
            }
            
            if (B64(modifier, expectedValue, value, out result))
            {
                return result;
            }
            
            if (IsRegex(modifier, expectedValue, value, canProcessRegex, onRegexFailure, out result))
            {
                return result;
            }
            
            if (Cased(modifier, expectedValue, value, out result))
            {
                return result;
            }
        
            if (NumericCompare(modifier, expectedValue, value, out result))
            {
                return result;
            }
            
            if (Exists(modifier, value, out result))
            {
                return result;
            }
            
            if (Expand(modifier, expectedValue, value, domainControllers, out result))
            {
                return result;
            }
        }
        
        if (FieldRefEquals(modifier, expected, expectedValue, aliasFactory, extractValue, out result))
        {
            return result;
        }
        
        if (FieldRefEndsWidth(modifier, expected, expectedValue, aliasFactory, extractValue, out result))
        {
            return result;
        }
        
        if (FieldRefStartsWith(modifier, expected, expectedValue, aliasFactory, extractValue, out result))
        {
            return result;
        }
        
        if (FieldRefContains(modifier, expected, expectedValue, aliasFactory, extractValue, out result))
        {
            return result;
        }
        
        return false;
    }

    private static IEnumerable<string?> GetValues((string? value, bool abnormalPattern) extractedValue)
    {
        if (extractedValue.abnormalPattern)
        {
            var allValues = extractedValue.value?.FromAbnormalPattern() ?? [];
            foreach (var value in allValues)
            {
                yield return value;
            }
        }
        else
        {
            yield return extractedValue.value;
        }
    }

    private static bool All(string key, string expectedValue, Func<IEnumerable<string>> getAllValues, out bool result)
    {
        result = false;
        if (key.Equals(Constants.All, StringComparison.Ordinal))
        {
            var values = getAllValues();
            result = values.Any(value => IsContained(expectedValue, value));
            return true;
        }

        return false;

        static bool IsContained(string expectedValue, string value)
        {
            if (TryWildcard(expectedValue, value, RegexOptions.IgnoreCase, out var res, patternModifier: pattern => pattern))
            {
                return res;
            }

            return value.Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool B64(ReadOnlySpan<char> modifier, string expectedValue, string? value, out bool result)
    {
        result = false;
        if (modifier.Contains(Constants.Base64, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (Base64Helper.TryGetBase64String(expectedValue, out var base64))
            {
                result = value.Contains(base64, StringComparison.OrdinalIgnoreCase);
                return true;
            }
        }

        return false;
    }
    
    private static bool Cased(ReadOnlySpan<char> modifier, string expectedValue, string? value, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.Cased, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (TryWildcard(expectedValue, value, RegexOptions.None, out result, patternModifier: pattern => $"^{pattern}$"))
            {
                return true;
            }
            
            result = value.Equals(expectedValue, StringComparison.Ordinal);
            return true;
        }

        return false;
    }

    private const string Ipv6Loopback = "::1";
    private const string Ipv6LoopbackWithMask = "::1/128";
    private static bool Cidr(ReadOnlySpan<char> modifier, string expectedValue, string? value, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.Cidr, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (!IPNetwork2.TryParse(value.Equals(Ipv6Loopback, StringComparison.OrdinalIgnoreCase) ? Ipv6LoopbackWithMask : value, out var ipAddress)) return true;
            if (!IPNetwork2.TryParse(expectedValue, out var expected)) return true;
            result = expected.Contains(ipAddress);
            return true;
        }

        return false;
    }
    
    private static bool Contains(ReadOnlySpan<char> modifier, string expectedValue, string? value, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.Contains, StringComparison.Ordinal) || modifier.Equals(Constants.ContainsAll, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (TryWildcard(expectedValue, value, RegexOptions.IgnoreCase, out result, patternModifier: pattern => pattern))
            {
                return true;
            }
            
            result = value.Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
            return true;
        }
        
        if (modifier.Equals(Constants.ContainsCased, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (TryWildcard(expectedValue, value, RegexOptions.None, out result, patternModifier: pattern => pattern))
            {
                return true;
            }
            
            result = value.Contains(expectedValue, StringComparison.Ordinal);
            return true;
        }
        
        if (modifier.Equals(Constants.ContainsWindash, StringComparison.Ordinal) || modifier.Equals(Constants.ContainsAllWindash, StringComparison.Ordinal))
        {
            if (value == null) return true;
            foreach (var permutation in WinDashPermutations(value))
            {
                if (TryWildcard(expectedValue, permutation, RegexOptions.IgnoreCase, out result, patternModifier: pattern => pattern))
                {
                    if (result) return true;
                }
            
                if (permutation.Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                }
            }
            
            return true;
        }

        return false;
    }

    private static string ReplaceFirstOccurrence(this string source, char search, ReadOnlySpan<char> replace)
    {
        var index = source.IndexOf(search, StringComparison.Ordinal);
        if (index < 0) return source;
        var sourceSpan = source.AsSpan();
        return string.Concat(sourceSpan[..index], replace, sourceSpan[(index + 1)..]);
    }

    private static IEnumerable<string> WinDashPermutations(string input)
    {
        yield return input;
        yield return input.ReplaceFirstOccurrence(Constants.Dash, Constants.SlashString);
        yield return input.ReplaceFirstOccurrence(Constants.Dash, Constants.HyphenString);
        yield return input.ReplaceFirstOccurrence(Constants.Dash, Constants.EnDashString);
        yield return input.ReplaceFirstOccurrence(Constants.Dash, Constants.EmDashString);
        yield return input.ReplaceFirstOccurrence(Constants.Dash, Constants.HorizontalBarString);
    }
    
    private static bool EndsWith(ReadOnlySpan<char> modifier, string expectedValue, string? value, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.EndsWith, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (TryWildcard(expectedValue, value, RegexOptions.IgnoreCase, out result, patternModifier: pattern => $"{pattern}$"))
            {
                return true;
            }
            
            result = value.EndsWith(expectedValue, StringComparison.OrdinalIgnoreCase);
            return true;
        }
        
        if (modifier.Equals(Constants.EndsWithCased, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (TryWildcard(expectedValue, value, RegexOptions.None, out result, patternModifier: pattern => $"{pattern}$"))
            {
                return true;
            }
            
            result = value.EndsWith(expectedValue, StringComparison.Ordinal);
            return true;
        }
        
        if (modifier.Equals(Constants.EndsWithWindash, StringComparison.Ordinal))
        {
            if (value == null) return true;
            foreach (var permutation in WinDashPermutations(value))
            {
                if (TryWildcard(expectedValue, permutation, RegexOptions.IgnoreCase, out result, patternModifier: pattern => $"{pattern}$"))
                {
                    if (result) return true;
                }
            
                if (permutation.EndsWith(expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                }
            }
            
            return true;
        }

        return false;
    }
    
    private static bool StartsWith(ReadOnlySpan<char> modifier, string expectedValue, string? value, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.StartsWith, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (TryWildcard(expectedValue, value, RegexOptions.IgnoreCase, out result, patternModifier: pattern => $"^{pattern}"))
            {
                return true;
            }
            
            result = value.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase);
            return true;
        }
        
        if (modifier.Equals(Constants.StartsWithCased, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (TryWildcard(expectedValue, value, RegexOptions.None, out result, patternModifier: pattern => $"^{pattern}"))
            {
                return true;
            }
            
            result = value.StartsWith(expectedValue, StringComparison.Ordinal);
            return true;
        }

        return false;
    }
    
    private static bool Exists(ReadOnlySpan<char> modifier, string? value, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.Exists, StringComparison.Ordinal))
        {
            result = value is not null;
            return true;
        }

        return false;
    }
    
    private static bool Expand(ReadOnlySpan<char> modifier, string expectedValue, string? value, ISet<string> domainControllers, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.Expand, StringComparison.Ordinal))
        {
            if (expectedValue.Equals("%DC-MACHINE-NAME%"))
            {
                if (value is not null && domainControllers.Contains($"{value.StripDollarSign()}"))
                {
                    result = true;
                }
            }

            return true;
        }

        return false;
    }
    
    private static bool FieldRefEquals(ReadOnlySpan<char> modifier, ReadOnlySpan<char> expected, string otherExpected, Func<ReadOnlySpan<char>, string?> aliasFactory, Func<ReadOnlySpan<char>, (string? value, bool abnormalPattern)> extractValue, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.EqualsField, StringComparison.Ordinal) || modifier.Equals(Constants.FieldRef, StringComparison.Ordinal))
        {
            var alias = aliasFactory(otherExpected);
            if (alias is not null)
            {
                otherExpected = alias;
            }
            
            var extractedValue = extractValue(expected);
            if (extractedValue.value == null) return true;

            var otherExtractedValue = extractValue(otherExpected);
            if (otherExtractedValue.value == null) return true;

            result = extractedValue.value.Equals(otherExtractedValue.value, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        return false;
    }
    
    private static bool FieldRefEndsWidth(ReadOnlySpan<char> modifier, ReadOnlySpan<char> expected, string otherExpected, Func<ReadOnlySpan<char>, string?> aliasFactory, Func<ReadOnlySpan<char>, (string? value, bool abnormalPattern)> extractValue, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.EndsWithField, StringComparison.Ordinal) || modifier.Equals(Constants.FieldRefEndsWith, StringComparison.Ordinal))
        {
            var alias = aliasFactory(otherExpected);
            if (alias is not null)
            {
                otherExpected = alias;
            }
            
            var extractedValue = extractValue(expected);
            if (extractedValue.value == null) return true;

            var otherExtractedValue = extractValue(otherExpected);
            if (otherExtractedValue.value == null) return true;

            result = extractedValue.value.EndsWith(otherExtractedValue.value, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        return false;
    }
    
    private static bool FieldRefStartsWith(ReadOnlySpan<char> modifier, ReadOnlySpan<char> expected, string otherExpected, Func<ReadOnlySpan<char>, string?> aliasFactory, Func<ReadOnlySpan<char>, (string? value, bool abnormalPattern)> extractValue, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.FieldRefStartsWith, StringComparison.Ordinal))
        {
            var alias = aliasFactory(otherExpected);
            if (alias is not null)
            {
                otherExpected = alias;
            }
            
            var extractedValue = extractValue(expected);
            if (extractedValue.value == null) return true;

            var otherExtractedValue = extractValue(otherExpected);
            if (otherExtractedValue.value == null) return true;

            result = extractedValue.value.StartsWith(otherExtractedValue.value, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        return false;
    }
    
    private static bool FieldRefContains(ReadOnlySpan<char> modifier, ReadOnlySpan<char> expected, string otherExpected, Func<ReadOnlySpan<char>, string?> aliasFactory, Func<ReadOnlySpan<char>, (string? value, bool abnormalPattern)> extractValue, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.FieldRefContains, StringComparison.Ordinal))
        {
            var alias = aliasFactory(otherExpected);
            if (alias is not null)
            {
                otherExpected = alias;
            }
            
            var extractedValue = extractValue(expected);
            if (extractedValue.value == null) return true;

            var otherExtractedValue = extractValue(otherExpected);
            if (otherExtractedValue.value == null) return true;

            result = extractedValue.value.Contains(otherExtractedValue.value, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        return false;
    }

    private static bool IsRegex(ReadOnlySpan<char> modifier, string expectedValue, string? value, Func<string, bool> canProcessRegex, Action<string> onRegexFailure, out bool result)
    {
        result = false;
        try
        {
            if (!canProcessRegex(expectedValue)) return false;
            if (modifier.Equals(Constants.Re, StringComparison.Ordinal))
            {
                if (value == null) return true;
                result = Regex.IsMatch(input: value, pattern: expectedValue);
                return true;
            }
        
            if (modifier.Equals(Constants.ReI, StringComparison.Ordinal))
            {
                if (value == null) return true;
                result = Regex.IsMatch(input: value, pattern: expectedValue, RegexOptions.IgnoreCase);
                return true;
            }
        
            if (modifier.Equals(Constants.ReM, StringComparison.Ordinal))
            {
                if (value == null) return true;
                result = Regex.IsMatch(input: value, pattern: expectedValue, RegexOptions.Multiline);
                return true;
            }
        
            if (modifier.Equals(Constants.ReS, StringComparison.Ordinal))
            {
                if (value == null) return true;
                result = Regex.IsMatch(input: value, pattern: expectedValue, RegexOptions.Singleline);
                return true;
            }
        }
        catch (Exception)
        {
            onRegexFailure(expectedValue);
            throw;
        }

        return false;
    }

    private static bool NumericCompare(ReadOnlySpan<char> modifier, string expectedValue, string? value, out bool result)
    {
        result = false;
        if (modifier.Equals(Constants.Gt, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (long.TryParse(value, out long longValue)) return true;
            if (long.TryParse(expectedValue, out long longExpectedValue)) return true;
            result = longValue > longExpectedValue;
            return true;
        }
        
        if (modifier.Equals(Constants.Gte, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (long.TryParse(value, out long longValue)) return true;
            if (long.TryParse(expectedValue, out long longExpectedValue)) return true;
            result = longValue >= longExpectedValue;
            return true;
        }
        
        if (modifier.Equals(Constants.Lt, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (long.TryParse(value, out long longValue)) return true;
            if (long.TryParse(expectedValue, out long longExpectedValue)) return true;
            result = longValue < longExpectedValue;
            return true;
        }
        
        if (modifier.Equals(Constants.Lte, StringComparison.Ordinal))
        {
            if (value == null) return true;
            if (long.TryParse(value, out long longValue)) return true;
            if (long.TryParse(expectedValue, out long longExpectedValue)) return true;
            result = longValue <= longExpectedValue;
            return true;
        }

        return false;
    } 
}