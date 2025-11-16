using System.Collections.Concurrent;
using BitFaster.Caching;
using BitFaster.Caching.Lru;

namespace Collector.Detection.Aggregations.Aggregators;

internal sealed class EventLruTracker
{
    private readonly ICache<long, byte> _lru;
    private readonly ConcurrentQueue<long> _deletedEventIds = new();

    public EventLruTracker(TimeSpan expiration, int maxEvents)
    {
        _lru = BuildLru(expiration, maxEvents);
        if (_lru.Events.Value is not null)
        {
            _lru.Events.Value.ItemRemoved += OnItemRemoved;
        }
    }
    
    public void OnWinEventInsert(long id)
    {
        _lru.AddOrUpdate(id, 0);
    }

    public void TrimExpired()
    {
        _lru.Policy.ExpireAfterWrite.Value?.TrimExpired();
    }

    public void Clear() => _lru.Clear();

    public ISet<long> GetDeletedEventIds()
    {
        var result = new HashSet<long>();
        while (_deletedEventIds.TryDequeue(out var id))
        {
            result.Add(id);
        }

        return result;
    }

    private void OnItemRemoved(object? sender, ItemRemovedEventArgs<long, byte> args)
    {
        _deletedEventIds.Enqueue(args.Key);
    }

    private static ICache<long, byte> BuildLru(TimeSpan timeframe, int capacity)
    {
        if (timeframe == Events.Constants.DefaultTimeFrame)
        {
            return new ConcurrentLruBuilder<long, byte>()
                .WithCapacity(capacity)
                .WithMetrics()
                .Build();
        }

        return new ConcurrentLruBuilder<long, byte>()
            .WithCapacity(capacity)
            .WithExpireAfterWrite(timeframe)
            .WithMetrics()
            .Build();
    }
}