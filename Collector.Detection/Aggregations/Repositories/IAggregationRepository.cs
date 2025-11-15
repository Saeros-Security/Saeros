using Collector.Detection.Aggregations.Interfaces;
using Collector.Detection.Rules;
using Shared;

namespace Collector.Detection.Aggregations.Repositories;

public interface IAggregationRepository
{
    Task InsertAsync(IDictionary<AggregationRule, IEnumerable<WinEvent>> aggregations, Action<AggregationRule, long, ISet<string>> onInsert, IProvideRuleProperties rulePropertiesProvider, CancellationToken cancellationToken);
    IEnumerable<WinEvent> Query(string ruleId, string query);
    Task DeleteAsync(string ruleId, ISet<long> ids, CancellationToken cancellationToken);
}