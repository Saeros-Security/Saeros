using Collector.Detection.Rules.Builders;
using Collector.Detection.Rules.Extensions;
using Shared.Extensions;

namespace Collector.Detection.Aggregations.Extensions;

internal static class EnumerableExtensions
{
    public static IEnumerable<string> ExtractProperties(this IEnumerable<string> properties)
    {
        foreach (var property in properties.Select(property => property.TakeLast(Constants.Dot)))
        {
            yield return property;
        }

        foreach (var property in WinEventExtensions.SystemColumns)
        {
            yield return property;
        }
    }
}