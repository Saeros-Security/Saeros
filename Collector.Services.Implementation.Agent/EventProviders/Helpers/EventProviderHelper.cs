using Collector.Core.EventProviders;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Implementation.Agent.EventProviders.Manifests;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Vanara.PInvoke;
using static Vanara.PInvoke.Tdh;

namespace Collector.Services.Implementation.Agent.EventProviders.Helpers;

internal static class EventProviderHelper
{
    private sealed record PublisherMetadata(string ProviderName, IDictionary<int, string> ChannelsById);
    
    private static IDictionary<string, ChannelType> GetChannelTypeByChannelName()
    {
        var channelTypeByChannelName = new Dictionary<string, ChannelType>(StringComparer.OrdinalIgnoreCase);
        const string channelsKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WINEVT\\Channels";
        using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var channelSubKey = localMachine.OpenSubKey(channelsKey, writable: false);
        if (channelSubKey == null) return channelTypeByChannelName;
        foreach (var channelName in channelSubKey.GetSubKeyNames())
        {
            using var channelNameSubKey = localMachine.OpenSubKey($@"{channelsKey}\{channelName}", writable: false);
            if (channelNameSubKey?.GetValue("Type") is not int type) continue;
            channelTypeByChannelName.TryAdd(channelName, (ChannelType)type);
        }

        return channelTypeByChannelName;
    }

    private static IDictionary<Guid, PublisherMetadata> GetPublisherMetadataByProviderGuid()
    {
        var publisherMetadataByProviderGuid = new Dictionary<Guid, PublisherMetadata>();
        const string publishersKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WINEVT\\Publishers";
        using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var publishersSubKey = localMachine.OpenSubKey(publishersKey, writable: false);
        if (publishersSubKey == null) return publisherMetadataByProviderGuid;
        foreach (var providerGuid in publishersSubKey.GetSubKeyNames())
        {
            if (!Guid.TryParse(providerGuid, out var guid)) continue;
            using var providerKey = localMachine.OpenSubKey($"{publishersKey}\\{providerGuid}", writable: false);
            if (providerKey?.GetValue(string.Empty) is not string providerName) continue;
            
            using var channelReferences = localMachine.OpenSubKey($"{publishersKey}\\{providerGuid}\\ChannelReferences", writable: false);
            if (channelReferences == null)
            {
                publisherMetadataByProviderGuid.Add(guid, new PublisherMetadata(providerName, ChannelsById: new Dictionary<int, string>()));
            }
            else
            {
                foreach (var channelReference in channelReferences.GetSubKeyNames())
                {
                    using var channelReferenceKey = channelReferences.OpenSubKey(channelReference, writable: false);
                    if (channelReferenceKey == null) continue;
                    
                    var id = channelReferenceKey.GetValue("Id");
                    var name = channelReferenceKey.GetValue(string.Empty);
                    if (id is not int channelId || name is not string channelName) continue;
                    if (publisherMetadataByProviderGuid.TryGetValue(guid, out var publisherMetadata))
                    {
                        publisherMetadata.ChannelsById[channelId] = channelName;
                    }
                    else
                    {
                        publisherMetadataByProviderGuid.Add(guid, new PublisherMetadata(providerName, ChannelsById: new Dictionary<int, string>
                        {
                            { channelId, channelName }
                        }));
                    }
                }
            }
        }

        return publisherMetadataByProviderGuid;
    }
    
    public static IEnumerable<KeyValuePair<ProviderManifest, ISet<EventManifest>>> EnumerateProviders(ILogger logger)
    {
        var channelTypeByChannelName = GetChannelTypeByChannelName();
        var channelsByProviderGuid = GetPublisherMetadataByProviderGuid();
        Win32Error.ThrowIfFailed(TdhEnumerateProviders(out var providerEnumerationInfo));
        foreach (var (providerGuid, schemaSource, providerName) in providerEnumerationInfo.Value.TraceProviderInfoArray.Select(traceProviderInfo => (traceProviderInfo.ProviderGuid, traceProviderInfo.SchemaSource, PEI_PROVIDER_NAME(providerEnumerationInfo, traceProviderInfo))))
        {
            if (string.IsNullOrEmpty(providerName)) continue;
            var mofProviderName = string.Empty;
            var eventManifests = new HashSet<EventManifest>();
            if (TdhEnumerateManifestProviderEvents(providerGuid, out var providerEventInfo).Succeeded)
            {
                var providerType = ToType(schemaSource);
                foreach (var eventDescriptor in providerEventInfo.EventDescriptorsArray)
                {
                    var unknownLengthProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        Win32Error.ThrowIfFailed(TdhGetManifestEventInformation(providerGuid, eventDescriptor, out var traceEventInfo));
                        foreach (var propertyEventInfo in traceEventInfo.Value.EventPropertyInfoArray)
                        {
                            var propertyName = TEI_PROPERTY_NAME(traceEventInfo, propertyEventInfo);
                            if (string.IsNullOrEmpty(propertyName)) continue;
                            if (propertyEventInfo.Flags.HasFlag(PROPERTY_FLAGS.PropertyParamFixedCount))
                            {
                                unknownLengthProperties.Add(propertyName.Trim());
                            }

                            if (propertyEventInfo.Flags.HasFlag(PROPERTY_FLAGS.PropertyParamCount) && propertyEventInfo.Flags.HasFlag(PROPERTY_FLAGS.PropertyStruct))
                            {
                                unknownLengthProperties.Add(propertyName.Trim());
                            }
                        }

                        if (channelsByProviderGuid.TryGetValue(providerGuid, out var publisherMetadata))
                        {
                            if (providerType == ProviderType.Manifest)
                            {
                                if (publisherMetadata.ChannelsById.TryGetValue(eventDescriptor.Channel, out var channelName))
                                {
                                    eventManifests.Add(new EventManifest(eventDescriptor.Id, eventDescriptor.Version, channelName, unknownLengthProperties));
                                }
                            }
                            else if (providerType == ProviderType.Mof)
                            {
                                mofProviderName = publisherMetadata.ProviderName;
                                foreach (var channel in publisherMetadata.ChannelsById)
                                {
                                    if (!channelTypeByChannelName.ContainsKey(channel.Value))
                                    {
                                        eventManifests.Add(new EventManifest(eventDescriptor.Id, eventDescriptor.Version, channel.Value, unknownLengthProperties));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error has occurred for event Id: {EventId}", eventDescriptor.Id);
                    }
                }

                if (providerType == ProviderType.Manifest)
                {
                    yield return new KeyValuePair<ProviderManifest, ISet<EventManifest>>(new ProviderManifest(providerName.Trim(), providerType, providerGuid), eventManifests);
                }
                else if (providerType == ProviderType.Mof && !string.IsNullOrWhiteSpace(mofProviderName))
                {
                    yield return new KeyValuePair<ProviderManifest, ISet<EventManifest>>(new ProviderManifest(mofProviderName.Trim(), providerType, providerGuid), eventManifests);
                }
            }
        }
    }

    private static ProviderType ToType(uint schemaSource)
    {
        if (schemaSource == 0) return ProviderType.Manifest;
        return ProviderType.Mof;
    }
}