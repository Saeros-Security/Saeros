using ConcurrentCollections;

namespace Collector.Core.Extensions;

public static class SetExtensions
{
    public static void AddRange<T>(this ISet<T> targetHashSet, IEnumerable<T> collection)
    {
        foreach (var element in collection)
        {
            targetHashSet.Add(element);
        }
    }
    
    public static void AddRange<T>(this ConcurrentHashSet<T> targetHashSet, IEnumerable<T> collection)
    {
        foreach (var element in collection)
        {
            targetHashSet.Add(element);
        }
    }
}