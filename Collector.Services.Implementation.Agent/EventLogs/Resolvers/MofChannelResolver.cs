using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Collector.Core.EventProviders;
using Shared.Extensions;

namespace Collector.Services.Implementation.Agent.EventLogs.Resolvers;

public static class MofChannelResolver
{
    private static readonly ConcurrentDictionary<Guid, string> ProviderByGuid = new();
    private const string Application = nameof(Application);
    private const string System = nameof(System);
    
    public static bool TryResolveMofChannel(string channelOrProvider, out Guid providerGuid, [MaybeNullWhen(false)] out string providerName, out ProviderType providerType)
    {
        providerGuid = Guid.Empty;
        providerName = null;
        providerType = ProviderType.Mof;
        if (channelOrProvider.Equals("Windows PowerShell", StringComparison.OrdinalIgnoreCase))
        {
            var guid = channelOrProvider.ToGuid();
            providerGuid = guid;
            providerType = ProviderType.Mof;
            providerName = ProviderByGuid.GetOrAdd(guid, "PowerShell");
            return true;
        }

        if (channelOrProvider.Equals("Windows Error Reporting", StringComparison.OrdinalIgnoreCase))
        {
            var guid = channelOrProvider.ToGuid();
            providerGuid = guid;
            providerType = ProviderType.Mof;
            providerName = ProviderByGuid.GetOrAdd(guid, Application);
            return true;
        }
        
        if (channelOrProvider.Equals("ESENT", StringComparison.OrdinalIgnoreCase))
        {
            var guid = channelOrProvider.ToGuid();
            providerGuid = guid;
            providerType = ProviderType.Mof;
            providerName = ProviderByGuid.GetOrAdd(guid, Application);
            return true;
        }
        
        if (channelOrProvider.Equals("MsiInstaller", StringComparison.OrdinalIgnoreCase))
        {
            var guid = channelOrProvider.ToGuid();
            providerGuid = guid;
            providerType = ProviderType.Mof;
            providerName = ProviderByGuid.GetOrAdd(guid, Application);
            return true;
        }

        if (channelOrProvider.Equals("TermDD", StringComparison.OrdinalIgnoreCase))
        {
            var guid = channelOrProvider.ToGuid();
            providerGuid = guid;
            providerType = ProviderType.Mof;
            providerName = ProviderByGuid.GetOrAdd(guid, System);
            return true;
        }
        
        if (channelOrProvider.Equals("NetLogon", StringComparison.OrdinalIgnoreCase))
        {
            var guid = channelOrProvider.ToGuid();
            providerGuid = guid;
            providerType = ProviderType.Mof;
            providerName = ProviderByGuid.GetOrAdd(guid, System);
            return true;
        }
        
        if (channelOrProvider.Equals("ScreenConnect", StringComparison.OrdinalIgnoreCase))
        {
            var guid = channelOrProvider.ToGuid();
            providerGuid = guid;
            providerType = ProviderType.Mof;
            providerName = ProviderByGuid.GetOrAdd(guid, Application);
            return true;
        }
        
        return false;
    }
    
    public static bool TryGetMofChannelName(Guid providerGuid, [MaybeNullWhen(false)] out string channelName)
    {
        return ProviderByGuid.TryGetValue(providerGuid, out channelName);
    }
}