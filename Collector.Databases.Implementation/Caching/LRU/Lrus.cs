using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Collector.Core.SystemAudits;
using Collector.Databases.Abstractions.Domain.Computers;
using Collector.Databases.Abstractions.Domain.Processes;
using Collector.Databases.Implementation.Caching.Series.Keys;
using Collector.Databases.Implementation.Caching.Series.Values;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Streaming;

namespace Collector.Databases.Implementation.Caching.LRU;

public static class Lrus
{
    public static readonly ICache<string, ComputerRecord> ActiveComputerByName = new ConcurrentLruBuilder<string, ComputerRecord>()
        .WithCapacity(640)
        .WithExpireAfterWrite(TimeSpan.FromSeconds(10))
        .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
        .Build();
    
    public static readonly ICache<string, byte> RemovedDomainsBarrier = new ConcurrentLruBuilder<string, byte>()
        .WithCapacity(128)
        .WithExpireAfterWrite(TimeSpan.FromSeconds(15))
        .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
        .Build();
    
    public static readonly IAsyncCache<string, ISet<string>> UserTokensByUserName = new ConcurrentLruBuilder<string, ISet<string>>()
        .WithCapacity(128)
        .WithExpireAfterAccess(expiration: TimeSpan.FromHours(1))
        .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
        .WithAtomicGetOrAdd()
        .AsAsyncCache()
        .Build();
    
    public static readonly ICache<string, string> WorkstationNameByIpAddress = new ConcurrentLruBuilder<string, string>()
        .WithCapacity(65536)
        .WithExpireAfterWrite(expiration: TimeSpan.FromDays(1))
        .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
        .Build();
    
    public static readonly IAsyncCache<string, string> IpAddressByWorkstationName = new ConcurrentLruBuilder<string, string>()
        .WithCapacity(65536)
        .WithExpireAfterWrite(expiration: TimeSpan.FromDays(1))
        .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
        .WithAtomicGetOrAdd()
        .AsAsyncCache()
        .Build();

    public static readonly ICache<SystemAuditKey, AuditStatus> AuditStatusByKey = new ConcurrentLruBuilder<SystemAuditKey, AuditStatus>()
        .WithCapacity(128)
        .WithExpireAfterWrite(TimeSpan.FromSeconds(15))
        .WithMetrics()
        .Build();
    
    public static readonly ICache<string, string> CommandLineByProcessId = new ConcurrentLruBuilder<string, string>()
        .WithCapacity(1024)
        .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
        .WithExpireAfterWrite(TimeSpan.FromSeconds(5))
        .Build();
    
    public static readonly ICache<string, string> ParentProcessIdByProcessId = new ConcurrentLruBuilder<string, string>()
        .WithCapacity(1024)
        .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
        .WithExpireAfterWrite(TimeSpan.FromSeconds(5))
        .Build();
    
    private sealed class NetworkExpiry : IExpiryCalculator<NetworkKey, NetworkValue>
    {
        public Duration GetExpireAfterCreate(NetworkKey key, NetworkValue value) => Duration.FromTimeSpan(value.ExpireIn);
        public Duration GetExpireAfterRead(NetworkKey key, NetworkValue value, Duration current) => current;
        public Duration GetExpireAfterUpdate(NetworkKey key, NetworkValue value, Duration current) => Duration.FromTimeSpan(value.ExpireIn);
    }
    
    public static readonly ICache<NetworkKey, NetworkValue> NetworkKeyByValue = new ConcurrentLruBuilder<NetworkKey, NetworkValue>()
        .WithCapacity(25)
        .WithExpireAfter(new NetworkExpiry())
        .Build();

    public static readonly ConcurrentLruBuilder<ProcessKey, ProcessRecord> ProcessByKeyBuilder = new ConcurrentLruBuilder<ProcessKey, ProcessRecord>()
        .WithCapacity(1024)
        .WithMetrics();
}