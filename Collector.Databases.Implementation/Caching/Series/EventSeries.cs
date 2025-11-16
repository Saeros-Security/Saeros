using System.Collections.Concurrent;
using Collector.Databases.Implementation.Caching.Series.Resolvers;
using MessagePack;
using Microsoft.Extensions.Logging;
using Polly;

namespace Collector.Databases.Implementation.Caching.Series;

public sealed class EventSeries(ILogger logger, string path) : ISeries
{
    private const string FileName = $"{nameof(EventSeries)}.bin";
    private readonly ConcurrentDictionary<int, long> _cache = new();
    private int _disposed;
    
    public void Insert(IDictionary<int, long> eventCountById)
    {
        foreach (var kvp in eventCountById)
        {
            _cache.AddOrUpdate(kvp.Key, kvp.Value, (_, current) => current + kvp.Value);
        }
    }

    public SortedDictionary<int, long> GetEventCountById()
    {
        var items = new SortedDictionary<int, long>();
        foreach (var kvp in _cache)
        {
            items.Add(kvp.Key, kvp.Value);
        }

        return items;
    }
    
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var filePath = Path.Join(path, FileName);
        var policy = Policy.Handle<Exception>().WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(1), onRetry: (exception, _) =>
        {
            logger.LogError(exception, "Could not deserialize {FileName}...", FileName);
        });
        
        try
        {
            await policy.ExecuteAsync(async ct =>
            {
                if (File.Exists(filePath))
                {
                    await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    foreach (var kvp in await MessagePackSerializer.DeserializeAsync<Dictionary<int, long>>(stream, SeriesMessagePackResolver.Instance.Options, ct))
                    {
                        _cache[kvp.Key] = kvp.Value;
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, comparand: 0) != 0) return;
        var filePath = Path.Join(path, FileName);
        var policy = Policy.Handle<Exception>().WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(1), onRetry: (exception, _) =>
        {
            logger.LogError(exception, "Could not serialize {FileName}...", FileName);
        });
        
        try
        {
            await policy.ExecuteAsync(async ct =>
            {
                await using var stream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                await MessagePackSerializer.SerializeAsync(stream, _cache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), SeriesMessagePackResolver.Instance.Options, cancellationToken: ct);
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
    }
}