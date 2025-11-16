namespace Collector.Databases.Implementation.Helpers;

public static class ISetExtensions
{
    public static ISet<T> Merge<T>(this ISet<T> set, ISet<T> value, int limit)
    {
        set.UnionWith(value);
        return set.Take(limit).ToHashSet();
    }
}