using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Collector.Core.EventProviders;
using Shared.Extensions;

namespace Collector.Services.Implementation.Agent.EventLogs.Resolvers;

public static class PollingChannelResolver
{
    private static readonly ConcurrentDictionary<Guid, ISet<string>> ProvidersByGuid = new();
    private const string Security = nameof(Security);
    private const string System = nameof(System);
    
    public static bool TryResolvePollingChannel(string channelOrProvider, ISet<int> eventIds, out Guid providerGuid, [MaybeNullWhen(false)] out string providerName, out ProviderType providerType)
    {
        providerGuid = Guid.Empty;
        providerName = null;
        providerType = ProviderType.Polling;
        if (channelOrProvider == Security && eventIds.Contains(1102)) // Event log cleared
        {
            var guid = channelOrProvider.ToGuid();
            ProvidersByGuid.AddOrUpdate(guid, addValueFactory: _ => new HashSet<string> { Security }, updateValueFactory: (_, current) =>
            {
                current.Add(Security);
                return current;
            });
            
            providerGuid = guid;
            providerType = ProviderType.Polling;
            providerName = Security;
            return true;
        }
        
        if (channelOrProvider == System && eventIds.Contains(104)) // Event log cleared
        {
            var guid = channelOrProvider.ToGuid();
            ProvidersByGuid.AddOrUpdate(guid, addValueFactory: _ => new HashSet<string> { System }, updateValueFactory: (_, current) =>
            {
                current.Add(System);
                return current;
            });
            
            providerGuid = guid;
            providerType = ProviderType.Polling;
            providerName = System;
            return true;
        }
        
        return false;
    }
    
    public static bool TryGetPollingChannelNames(Guid providerGuid, [MaybeNullWhen(false)] out ISet<string> channelNames)
    {
        return ProvidersByGuid.TryGetValue(providerGuid, out channelNames);
    }
}