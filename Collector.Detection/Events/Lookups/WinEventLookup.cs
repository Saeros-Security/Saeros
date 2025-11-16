using System.Diagnostics.CodeAnalysis;
using Collector.Detection.Rules.Extensions;
using Shared;

namespace Collector.Detection.Events.Lookups;

internal static class WinEventLookup
{
    public static IEnumerable<string> EnumerateStringValues(this WinEvent winEvent)
    {
        foreach (var systemValue in EnumerateStringValuesInternal(winEvent.System))
        {
            yield return systemValue;
        }
        
        foreach (var eventDataValue in EnumerateStringValuesInternal(winEvent.EventData))
        {
            yield return eventDataValue;
        }
    }

    private static IEnumerable<string> EnumerateStringValuesInternal(IDictionary<string, string> dictionary)
    {
        foreach (var pair in dictionary)
        {
            if (pair.Value.Contains(Rules.Builders.Constants.AbnormalSeparator, StringComparison.Ordinal))
            {
                foreach (var abnormalItem in pair.Value.FromAbnormalPattern())
                {
                    yield return abnormalItem;
                }
            }
            else
            {
                yield return pair.Value;
            }
        }
    }
    
    public static string? GetValue(this WinEvent winEvent, string propertyName)
    {
        if (TryGetValueInternal(winEvent.System, propertyName, out var systemValue))
        {
            return systemValue;
        }
        
        if (TryGetValueInternal(winEvent.EventData, propertyName, out var eventDataValue))
        {
            return eventDataValue;
        }

        return null;
    }
    
    private static bool TryGetValueInternal(IDictionary<string, string> dictionary, string propertyName, [MaybeNullWhen(false)] out string value)
    {
        value = null;
        if (dictionary.TryGetValue(propertyName, out var propertyValue))
        {
            value = propertyValue;
            return true;
        }

        return false;
    }
    
    public static string? GetValue(this WinEvent winEvent, ReadOnlySpan<char> propertyName, out bool abnormalPattern)
    {
        if (TryGetValueInternal(winEvent.System, propertyName, out abnormalPattern, out var systemValue))
        {
            return systemValue;
        }
        
        if (TryGetValueInternal(winEvent.EventData, propertyName, out abnormalPattern, out var eventDataValue))
        {
            return eventDataValue;
        }

        return null;
    }

    private static bool TryGetValueInternal(IDictionary<string, string> dictionary, ReadOnlySpan<char> propertyName, out bool abnormalPattern, [MaybeNullWhen(false)] out string value)
    {
        abnormalPattern = false;
        value = null;
        if (dictionary.TryGetValue(propertyName, out var propertyValue))
        {
            abnormalPattern = propertyValue.Contains(Rules.Builders.Constants.AbnormalSeparator, StringComparison.Ordinal);
            value = propertyValue;
            return true;
        }

        return false;
    }
    
    public static bool Contain(this WinEvent winEvent, Func<string, bool> contains)
    {
        foreach (var pair in winEvent.System)
        {
            if (contains(pair.Value)) return true;
        }
        
        foreach (var pair in winEvent.EventData)
        {
            if (contains(pair.Value)) return true;
        }

        return false;
    }
}