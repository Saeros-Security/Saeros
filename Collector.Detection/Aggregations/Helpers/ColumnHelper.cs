using Shared;

namespace Collector.Detection.Aggregations.Helpers;

internal static class ColumnHelper
{
    public static ISet<string> ExtractColumns(IEnumerable<WinEvent> winEvents, ISet<string> ruleProperties)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var winEvent in winEvents)
        {
            foreach (var pair in winEvent.System)
            {
                if (ruleProperties.Contains(pair.Key))
                {
                    columns.Add(pair.Key);
                }
            }

            foreach (var pair in winEvent.EventData)
            {
                if (ruleProperties.Contains(pair.Key))
                {
                    columns.Add(pair.Key);
                }
            }
        }

        return columns;
    }

    public static IEnumerable<KeyValuePair<string, string>> ExtractColumns(WinEvent winEvent, ISet<string> ruleProperties)
    {
        foreach (var pair in winEvent.System)
        {
            if (ruleProperties.Contains(pair.Key))
            {
                yield return new KeyValuePair<string, string>(pair.Key, pair.Value);
            }
        }

        foreach (var pair in winEvent.EventData)
        {
            if (ruleProperties.Contains(pair.Key))
            {
                yield return new KeyValuePair<string, string>(pair.Key, pair.Value);
            }
        }
    }
}