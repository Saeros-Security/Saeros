using Shared;

namespace Collector.Detection.Aggregations.Interfaces;

public interface IAggregator
{
    WinEvent Matched(string ruleId, WinEvent match);
    IEnumerable<WinEvent> Query(string ruleId, string query);
    bool ContainsColumn(string ruleId, string column);
}