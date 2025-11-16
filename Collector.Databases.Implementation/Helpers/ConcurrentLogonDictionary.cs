using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Collector.Databases.Abstractions.Stores.Logon;

namespace Collector.Databases.Implementation.Helpers;

public sealed class ConcurrentLogonDictionary
{
    private readonly ICache<string, AccountLogon> _lru;
    private readonly Lock _lock = new();
    private readonly int _capacity;
    
    public ConcurrentLogonDictionary(int capacity, TimeSpan eviction)
    {
        _capacity = capacity;
        _lru = new ConcurrentLruBuilder<string, AccountLogon>()
            .WithCapacity(capacity * 2)
            .WithExpireAfterWrite(eviction)
            .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
            .Build();
    }
    
    public ConcurrentLogonDictionary(int capacity)
    {
        _capacity = capacity;
        _lru = new ConcurrentLruBuilder<string, AccountLogon>()
            .WithCapacity(capacity * 2)
            .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
            .Build();
    }

    public void AddOrUpdate(AccountLogon key, bool cumulative)
    {
        lock (_lock)
        {
            if (_lru.Count == _capacity - 1 && !_lru.TryGet(key.TargetAccount, out _))
            {
                var min = _lru.MinBy(kvp => kvp.Value.Count);
                if (min.Value.Count < key.Count)
                {
                    _lru.TryRemove(min);
                }
                else
                {
                    _lru.Policy.ExpireAfterWrite.Value?.TrimExpired();
                    return;
                }
            }

            _lru.AddOrUpdate(key.TargetAccount, _lru.TryGet(key.TargetAccount, out var currentValue) ? new AccountLogon(key.TargetAccount, currentValue.TargetComputer.Merge(key.TargetComputer, limit: 5), currentValue.LogonType.Merge(key.LogonType, limit: 5), currentValue.SourceComputer.Merge(key.SourceComputer, limit: 5), currentValue.SourceIpAddress.Merge(key.SourceIpAddress, limit: 5), count: cumulative ? key.Count : currentValue.Count + 1) : new AccountLogon(key.TargetAccount, key.TargetComputer, key.LogonType, key.SourceComputer, key.SourceIpAddress, key.Count));
        }
    }

    public IEnumerable<AccountLogon> Enumerate()
    {
        lock (_lock)
        {
            return _lru.Select(kvp => kvp.Value).OrderByDescending(kvp => kvp.Count).ToList();
        }
    }
}