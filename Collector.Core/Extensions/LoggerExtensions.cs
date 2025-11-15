using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Collector.Core.Extensions;

public static class LoggerExtensions
{
    private static readonly IMemoryCache MemoryCache = new MemoryCache(new MemoryCacheOptions());

    public static void Throttle(this ILogger logger, string key, Action<ILogger> log, TimeSpan expiration)
    {
        if (!MemoryCache.TryGetValue(key, out _))
        {
            log(logger);
            MemoryCache.Set(key, (byte)0, absoluteExpirationRelativeToNow: expiration);
        }
    }
}