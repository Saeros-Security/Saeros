namespace Collector.Detection.Rules.Extensions;

internal static class SetExtensions
{
    public static bool Contains(this HashSet<string> set, ReadOnlySpan<char> key)
    {
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.Contains(key);
    }
}