using Collector.Detection.Rules;
using Shared;

namespace Collector.Detection.Aggregations.Interfaces;

public interface IPreAggregator
{
    Task TrimExpiredAsync(IDictionary<AggregationRule, IEnumerable<WinEvent>> aggregations, CancellationToken cancellationToken);
    Task AddAsync(IDictionary<AggregationRule, IEnumerable<WinEvent>> aggregations, CancellationToken cancellationToken);
}