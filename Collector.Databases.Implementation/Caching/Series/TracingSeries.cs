using System.Collections.Concurrent;
using Collector.Databases.Implementation.Caching.Series.Resolvers;
using MessagePack;
using Microsoft.Extensions.Logging;
using Polly;

namespace Collector.Databases.Implementation.Caching.Series;

public sealed class TracingSeries(ILogger logger, string path) : ISeries
{
    private const string FileName = $"{nameof(TracingSeries)}.bin";
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cache = new(StringComparer.OrdinalIgnoreCase);
    private int _disposed;

    public void Insert(string hash, DateTimeOffset date)
    {
        _cache.TryAdd(hash, date);
    }
    
    public void Remove(string hash)
    {
        _cache.TryRemove(hash, out _);
    }

    public bool Contains(string hash)
    {
        return _cache.ContainsKey(hash);
    }

    public IEnumerable<KeyValuePair<string, DateTimeOffset>> Enumerate()
    {
        return _cache;
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
                    foreach (var kvp in await MessagePackSerializer.DeserializeAsync<Dictionary<string, DateTimeOffset>>(stream, SeriesMessagePackResolver.Instance.Options, ct))
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