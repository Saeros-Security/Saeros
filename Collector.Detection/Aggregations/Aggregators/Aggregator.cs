using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Collector.Detection.Aggregations.Interfaces;
using Collector.Detection.Aggregations.Repositories;
using Collector.Detection.Rules;
using Shared;

namespace Collector.Detection.Aggregations.Aggregators;

public sealed class Aggregator(IAggregationRepository aggregationRepository, IProvideRuleProperties rulePropertiesProvider, int maxEventsPerRule = 65536) : IAggregator, IPreAggregator
{
    private readonly ConcurrentDictionary<string, Lazy<EventLruTracker>> _lruTrackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ISet<string>> _columnsByRuleId = new(StringComparer.OrdinalIgnoreCase);
    
    public WinEvent Matched(string ruleId, WinEvent match)
    {
        if (_lruTrackers.TryGetValue(ruleId, out var cache))
        {
            cache.Value.Clear();
        }

        return match;
    }

    public bool ContainsColumn(string ruleId, string column) => _columnsByRuleId.TryGetValue(ruleId, out var columns) && columns.Contains(column);
    
    public IEnumerable<WinEvent> Query(string ruleId, string query)
    {
        return aggregationRepository.Query(ruleId, query);
    }

    public Task TrimExpiredAsync(IDictionary<AggregationRule, IEnumerable<WinEvent>> aggregations, CancellationToken cancellationToken)
    {
        return Task.WhenAll(aggregations.Keys.Select(aggregationRule =>
        {
            if (_lruTrackers.TryGetValue(aggregationRule.Id, out var cache))
            {
                cache.Value.TrimExpired();
                var deletedEventIds = cache.Value.GetDeletedEventIds();
                if (deletedEventIds.Count > 0)
                {
                    return aggregationRepository.DeleteAsync(aggregationRule.Id, deletedEventIds, cancellationToken);
                }
            }
            
            return Task.CompletedTask;
        }));
    }

    public Task AddAsync(IDictionary<AggregationRule, IEnumerable<WinEvent>> aggregations, CancellationToken cancellationToken)
    {
        return aggregationRepository.InsertAsync(aggregations, onInsert: OnWinEventInsert, rulePropertiesProvider, cancellationToken);
    }
    
    private void OnWinEventInsert(AggregationRule aggregationRule, long id, ISet<string> columns)
    {
        var cache = _lruTrackers.GetOrAdd(aggregationRule.Id, valueFactory: _ => new Lazy<EventLruTracker>(() => new EventLruTracker(aggregationRule.CorrelationOrAggregationTimeSpan, maxEventsPerRule), LazyThreadSafetyMode.ExecutionAndPublication));
        cache.Value.OnWinEventInsert(id);
        
        _columnsByRuleId.AddOrUpdate(aggregationRule.Id, addValueFactory: _ => columns, updateValueFactory:
            (_, current) =>
            {
                foreach (var column in columns)
                {
                    current.Add(column);
                }

                return current;
            });
    }

    [field: AllowNull, MaybeNull]
    public static Aggregator Instance
    {
        get => field ?? throw new NullReferenceException(nameof(Instance));
        set;
    }
}