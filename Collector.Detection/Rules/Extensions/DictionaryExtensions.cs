using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Collector.Detection.Rules.Builders;

namespace Collector.Detection.Rules.Extensions;

internal static class DictionaryExtensions
{
    private static string SanitizeKey(string key)
    {
        return new string(key.TakeWhile(c => c != Constants.Pipe).ToArray());
    }

    public static IEnumerable<string> EnumerateKeys(this IDictionary<string, object> root, IDictionary<string, string> aliases)
    {
        foreach (var key in EnumerateKeys(root))
        {
            if (char.IsUpper(key.ElementAt(0)))
            {
                var sanitizedKey = SanitizeKey(key);
                if (aliases.TryGetValue(sanitizedKey, out var alias))
                {
                    yield return alias;
                }
                else
                {
                    yield return sanitizedKey;
                }
            }
        }
    }
    
    private static IEnumerable<string> EnumerateKeys(IEnumerable<object> keys)
    {
        foreach (var key in keys)
        {
            if (key is IEnumerable<object> enumerable)
            {
                foreach (var nestedKey in EnumerateKeys(enumerable))
                {
                    yield return nestedKey;
                }
            }
            else if (key is IDictionary dictionary)
            {
                switch (dictionary)
                {
                    case IDictionary<string, object> objectValue:
                        foreach (var nestedKey in EnumerateKeys(objectValue))
                        {
                            yield return nestedKey;
                        }
                        
                        break;
                    case IDictionary<string, string> stringValue:
                        foreach (var nestedKey in EnumerateKeys(stringValue.ToDictionary(kvp => kvp.Key, object (kvp) => kvp.Value, StringComparer.Ordinal)))
                        {
                            yield return nestedKey;
                        }
                        
                        break;
                    default:
                        throw new Exception($"Value of type {dictionary.GetType()} is not supported");
                }
            }
            else if (key is KeyValuePair<string, object> pair)
            {
                yield return pair.Key;
                
                if (pair.Value is IEnumerable<object> enumerableValue)
                {
                    foreach (var nestedKey in EnumerateKeys(enumerableValue.Select(prop => new KeyValuePair<string, object>(pair.Key, prop)).Cast<object>()))
                    {
                        yield return nestedKey;
                    }
                }
                else if (pair.Value is IDictionary innerDictionary)
                {
                    switch (innerDictionary)
                    {
                        case IDictionary<string, object> objectValue:
                            foreach (var nestedKey in EnumerateKeys(objectValue))
                            {
                                yield return nestedKey;
                            }
                            
                            break;
                        case IDictionary<string, string> stringValue:
                            foreach (var nestedKey in EnumerateKeys(stringValue.ToDictionary(kvp => kvp.Key, object (kvp) => kvp.Value, StringComparer.Ordinal)))
                            {
                                yield return nestedKey;
                            }
                            
                            break;
                        default:
                            throw new Exception($"Value of type {innerDictionary.GetType()} is not supported");
                    }
                }
            }
        }
    }
    
    private static IEnumerable<string> EnumerateKeys(this IDictionary<string, object> root)
    {
        foreach (var pair in root)
        {
            yield return pair.Key;
            
            if (pair.Value is IEnumerable<object> enumerable)
            {
                foreach (var nestedKey in EnumerateKeys(enumerable.Select(prop => new KeyValuePair<string, object>(pair.Key, prop)).Cast<object>()))
                {
                    yield return nestedKey;
                }
            }
            else if (pair.Value is IDictionary dictionary)
            {
                switch (dictionary)
                {
                    case IDictionary<string, object> objectValue:
                        foreach (var nestedKey in EnumerateKeys(objectValue))
                        {
                            yield return nestedKey;
                        }
                        
                        break;
                    case IDictionary<string, string> stringValue:
                        foreach (var nestedKey in EnumerateKeys(stringValue.ToDictionary(kvp => kvp.Key, object (kvp) => kvp.Value, StringComparer.Ordinal)))
                        {
                            yield return nestedKey;
                        }
                        
                        break;
                    default:
                        throw new Exception($"Value of type {dictionary.GetType()} is not supported");
                }
            }
        }
    }

    public static bool TryGetValue(this IDictionary<string, string> dictionary, ReadOnlySpan<char> key, [MaybeNullWhen(false)] out string value)
    {
        value = null;
        if (dictionary is Dictionary<string, string> casted)
        {
            var lookup = casted.GetAlternateLookup<ReadOnlySpan<char>>();
            return lookup.TryGetValue(key, out value);
        }

        return dictionary.TryGetValue(new string(key), out value);
    }
}