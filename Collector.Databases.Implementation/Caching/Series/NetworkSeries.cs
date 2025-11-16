using System.Collections.Concurrent;
using Collector.Core.Extensions;
using Collector.Databases.Implementation.Caching.LRU;
using Collector.Databases.Implementation.Caching.Series.Keys;
using Collector.Databases.Implementation.Caching.Series.Resolvers;
using Collector.Databases.Implementation.Caching.Series.Values;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Kernel;
using MessagePack;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Models.Console.Responses;

namespace Collector.Databases.Implementation.Caching.Series;

public sealed class NetworkSeries(ILogger logger, string path) : ISeries
{
    private const string FileName = $"{nameof(NetworkSeries)}.bin";
    private int _disposed;

    public void Insert(NetworkTracer networkTracer)
    {
        var key = new NetworkKey(networkTracer.Computer, networkTracer.ProcessName);
        var value = new NetworkValue(id: key.ToString(), networkTracer.Outbound, networkTracer.Countries, expiration: DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(1)));
        if (Lrus.NetworkKeyByValue.TryGet(key, out var currentValue))
        {
            currentValue.Outbound += value.Outbound;
            currentValue.Countries.AddRange(value.Countries);
            currentValue.SlideExpiration(TimeSpan.FromHours(1)); 
        }
        else
        {
            Lrus.NetworkKeyByValue.AddOrUpdate(key, value);
        }
    }

    private IEnumerable<KeyValuePair<NetworkKey, NetworkValue>> Enumerate()
    {
        foreach (var kvp in Lrus.NetworkKeyByValue)
        {
            yield return new KeyValuePair<NetworkKey, NetworkValue>(kvp.Key, kvp.Value);
        }
    }

    public (IEnumerable<OutboundEntry> Entries, IDictionary<string, long> OutboundByCountry) GetValues()
    {
        var outboundByCountries = new ConcurrentDictionary<string, long>();
        var entries = new List<OutboundEntry>();
        foreach (var kvp in Enumerate().OrderByDescending(item => item.Value.Outbound))
        {
            foreach (var country in kvp.Value.Countries)
            {
                outboundByCountries.AddOrUpdate(country, addValue: kvp.Value.Outbound, updateValueFactory: (_, current) =>
                {
                    current += kvp.Value.Outbound;
                    return current;
                });
            }
            
            entries.Add(new OutboundEntry(kvp.Key.Computer, kvp.Key.ProcessName, kvp.Value.Outbound));
        }

        return (entries, outboundByCountries);
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
                    foreach (var kvp in await MessagePackSerializer.DeserializeAsync<Dictionary<NetworkKey, NetworkValue>>(stream, SeriesMessagePackResolver.Instance.Options, ct))
                    {
                        Lrus.NetworkKeyByValue.AddOrUpdate(kvp.Key, kvp.Value);
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
                await MessagePackSerializer.SerializeAsync(stream, Lrus.NetworkKeyByValue.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), SeriesMessagePackResolver.Instance.Options, cancellationToken: ct);
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
    }
}