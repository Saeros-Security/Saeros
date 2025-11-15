using System.Diagnostics.CodeAnalysis;
using Collector.Core.EventProviders;

namespace Collector.Services.Abstractions.EventProviders;

public interface IEventProviderServiceReader
{
    bool TryGetProviderName(Guid providerGuid, [MaybeNullWhen(false)] out string providerName);
    bool TryGetMofChannelName(Guid providerGuid, [MaybeNullWhen(false)] out string channelName);
    bool TryGetManifestChannelName(Guid providerGuid, int eventId, [MaybeNullWhen(false)] out string channelName);
    bool TryGetManifestChannelNames(Guid providerGuid, [MaybeNullWhen(false)] out ISet<string> channelNames);
    bool TryGetUnknownLengthProperties(Guid providerGuid, int eventId, [MaybeNullWhen(false)] out ISet<string> unknownLengthProperties);
    bool TryResolveProvider(string channelOrProvider, ISet<int> eventIds, out Guid providerGuid, [MaybeNullWhen(false)] out string providerName, out ProviderType providerType, out bool userTrace);
}