using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Collector.Core.EventProviders;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Implementation.Agent.EventLogs.Resolvers;
using Collector.Services.Implementation.Agent.EventProviders.Helpers;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Agent.EventProviders;

public sealed class EventProviderService(ILogger<EventProviderService> logger) : IEventProviderServiceReader, IEventProviderServiceWriter
{
    private readonly Regex _channelRegex = new("(?=\\S*[-])([a-zA-Z-\\s]+)", RegexOptions.Compiled);
    private readonly IDictionary<Guid, ProviderType> _providerTypeByProviderGuid = new Dictionary<Guid, ProviderType>();
    private readonly IDictionary<Guid, ISet<string>> _manifestedChannelNamesByProviderGuid = new Dictionary<Guid, ISet<string>>();
    private readonly IDictionary<Guid, string> _providerNameByProviderGuid = new Dictionary<Guid, string>();
    private readonly IDictionary<string, Guid> _providerGuidByChannelName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, Guid> _providerGuidByProviderName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<Guid, IDictionary<int, string>> _manifestedChannelsByEventIdProviderGuid = new Dictionary<Guid, IDictionary<int, string>>();
    private readonly IDictionary<Guid, string> _mofChannelByProviderGuid = new Dictionary<Guid, string>();
    private readonly IDictionary<Guid, IDictionary<int, ISet<string>>> _unknownLengthPropertiesByEventIdByProviderGuid = new Dictionary<Guid, IDictionary<int, ISet<string>>>();

    public void LoadProviders(CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading providers...");
        foreach (var providerManifest in EventProviderHelper.EnumerateProviders(logger))
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _providerTypeByProviderGuid.TryAdd(providerManifest.Key.ProviderGuid, providerManifest.Key.ProviderType);
            _providerGuidByProviderName.TryAdd(providerManifest.Key.ProviderName, providerManifest.Key.ProviderGuid);
            _providerNameByProviderGuid.TryAdd(providerManifest.Key.ProviderGuid, providerManifest.Key.ProviderName);
            foreach (var eventManifest in providerManifest.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (providerManifest.Key.ProviderType == ProviderType.Manifest)
                {
                    _providerGuidByChannelName.TryAdd(eventManifest.Channel, providerManifest.Key.ProviderGuid);
                    if (_manifestedChannelNamesByProviderGuid.TryGetValue(providerManifest.Key.ProviderGuid, out var channelNames))
                    {
                        channelNames.Add(eventManifest.Channel);
                    }
                    else
                    {
                        _manifestedChannelNamesByProviderGuid.TryAdd(providerManifest.Key.ProviderGuid, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { eventManifest.Channel });
                    }

                    if (_manifestedChannelsByEventIdProviderGuid.TryGetValue(providerManifest.Key.ProviderGuid, out var channels))
                    {
                        channels.TryAdd(eventManifest.EventId, eventManifest.Channel);
                    }
                    else
                    {
                        _manifestedChannelsByEventIdProviderGuid.TryAdd(providerManifest.Key.ProviderGuid, new Dictionary<int, string> { { eventManifest.EventId, eventManifest.Channel } });
                    }
                }
                else if (providerManifest.Key.ProviderType == ProviderType.Mof)
                {
                    _mofChannelByProviderGuid.TryAdd(providerManifest.Key.ProviderGuid, eventManifest.Channel);
                }

                if (_unknownLengthPropertiesByEventIdByProviderGuid.TryGetValue(providerManifest.Key.ProviderGuid, out var unknownLengthPropertiesByEventKey))
                {
                    unknownLengthPropertiesByEventKey.TryAdd(eventManifest.EventId, eventManifest.UnknownLengthProperties);
                }
                else
                {
                    _unknownLengthPropertiesByEventIdByProviderGuid.TryAdd(providerManifest.Key.ProviderGuid, new Dictionary<int, ISet<string>> { { eventManifest.EventId, eventManifest.UnknownLengthProperties } });
                }
            }
        }

        logger.LogInformation("Loaded {Count} providers", _providerGuidByProviderName.Count);
    }

    private bool TryGetProviderGuidByChannelName(string channelName, out Guid providerGuid)
    {
        return _providerGuidByChannelName.TryGetValue(channelName, out providerGuid);
    }

    private bool TryGetProviderGuidByProviderName(string providerName, out Guid providerGuid)
    {
        return _providerGuidByProviderName.TryGetValue(providerName, out providerGuid);
    }

    public bool TryGetProviderName(Guid providerGuid, [MaybeNullWhen(false)] out string providerName)
    {
        return _providerNameByProviderGuid.TryGetValue(providerGuid, out providerName);
    }

    public bool TryGetMofChannelName(Guid providerGuid, [MaybeNullWhen(false)] out string channelName)
    {
        return _mofChannelByProviderGuid.TryGetValue(providerGuid, out channelName);
    }

    public bool TryGetManifestChannelName(Guid providerGuid, int eventId, [MaybeNullWhen(false)] out string channelName)
    {
        channelName = null;
        if (_manifestedChannelsByEventIdProviderGuid.TryGetValue(providerGuid, out var channels))
        {
            return channels.TryGetValue(eventId, out channelName);
        }

        return false;
    }

    public bool TryGetManifestChannelNames(Guid providerGuid, [MaybeNullWhen(false)] out ISet<string> channelNames)
    {
        return _manifestedChannelNamesByProviderGuid.TryGetValue(providerGuid, out channelNames);
    }

    public bool TryGetUnknownLengthProperties(Guid providerGuid, int eventId, [MaybeNullWhen(false)] out ISet<string> unknownLengthProperties)
    {
        unknownLengthProperties = null;
        if (_unknownLengthPropertiesByEventIdByProviderGuid.TryGetValue(providerGuid, out var unknownLengthPropertiesByEventKey))
        {
            return unknownLengthPropertiesByEventKey.TryGetValue(eventId, out unknownLengthProperties);
        }

        return false;
    }
    
    public bool TryResolveProvider(string channelOrProvider, ISet<int> eventIds, out Guid providerGuid, [MaybeNullWhen(false)] out string providerName, out ProviderType providerType, out bool userTrace)
    {
        userTrace = true; // For the moment we only use User traces
        providerType = ProviderType.Mof;
        providerGuid = Guid.Empty;
        providerName = null;
        if (TryResolveChannel(channelOrProvider, eventIds, out providerGuid, out providerName, out providerType))
        {
            return true;
        }

        if (TryGetProviderGuidByChannelName(channelOrProvider, out providerGuid))
        {
            if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
            {
                return true;
            }
        }

        if (TryGetProviderGuidByProviderName(channelOrProvider, out providerGuid))
        {
            if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
            {
                return true;
            }
        }

        return false;
    }
    
    private bool TryGetProviderType(Guid providerGuid, out ProviderType providerType)
    {
        return _providerTypeByProviderGuid.TryGetValue(providerGuid, out providerType);
    }

    private bool TryResolveChannel(string channelOrProvider, ISet<int> eventIds, out Guid providerGuid, [MaybeNullWhen(false)] out string providerName, out ProviderType providerType)
    {
        providerType = ProviderType.Mof;
        providerGuid = Guid.Empty;
        providerName = null;
        if (PollingChannelResolver.TryResolvePollingChannel(channelOrProvider, eventIds, out providerGuid, out providerName, out providerType))
        {
            return true;
        }

        if (MofChannelResolver.TryResolveMofChannel(channelOrProvider, out providerGuid, out providerName, out providerType))
        {
            return true;
        }
        
        if (channelOrProvider.Contains("Microsoft-Windows-DNS Client Events", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetProviderGuidByChannelName("Microsoft-Windows-DNS-Client/Operational", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
        }
        
        if (channelOrProvider.Contains("Microsoft-Windows-Security-Mitigations", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetProviderGuidByChannelName("Microsoft-Windows-Security-Mitigations/UserMode", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
        }
        
        if (channelOrProvider.Equals("Audit-CVE", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetProviderGuidByProviderName("Microsoft-Windows-Audit-CVE", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
        }
        
        if (channelOrProvider.Equals("Kerberos-Key-Distribution-Center", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetProviderGuidByProviderName("Microsoft-Windows-Kerberos-Key-Distribution-Center", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
        }
        
        if (channelOrProvider.Equals("Microsoft-Windows-DHCP-Server", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetProviderGuidByChannelName("Microsoft-Windows-DHCP Server Events/Operational", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
        }
        
        if (channelOrProvider.Contains("Microsoft-Windows-AppXDeploymentServer", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetProviderGuidByChannelName("Microsoft-Windows-AppXDeploymentServer/Operational", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
        }
        
        var match = _channelRegex.Match(channelOrProvider);
        if (match.Success)
        {
            if (channelOrProvider.Contains($"/{Enum.GetName(ChannelType.Admin)}", StringComparison.OrdinalIgnoreCase) && TryGetProviderGuidByChannelName($"{match.Value}/{Enum.GetName(ChannelType.Admin)}", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
            
            if (channelOrProvider.Contains($"/{Enum.GetName(ChannelType.Operational)}", StringComparison.OrdinalIgnoreCase) && TryGetProviderGuidByChannelName($"{match.Value}/{Enum.GetName(ChannelType.Operational)}", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
            
            if (channelOrProvider.Contains($"/{Enum.GetName(ChannelType.Analytic)}", StringComparison.OrdinalIgnoreCase) && TryGetProviderGuidByChannelName($"{match.Value}/{Enum.GetName(ChannelType.Analytic)}", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
            
            if (channelOrProvider.Contains($"/{Enum.GetName(ChannelType.Debug)}", StringComparison.OrdinalIgnoreCase) && TryGetProviderGuidByChannelName($"{match.Value}/{Enum.GetName(ChannelType.Debug)}", out providerGuid))
            {
                if (TryGetProviderType(providerGuid, out providerType) && TryGetProviderName(providerGuid, out providerName))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
}