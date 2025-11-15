using System.Diagnostics.CodeAnalysis;
using Collector.Core.EventProviders;
using Collector.Services.Abstractions.EventLogs.Pipelines;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Abstractions.Tracing;
using Collector.Services.Implementation.Agent.EventLogs.Resolvers;
using Shared;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers;

public abstract class EventLogConsumer(IRuleService ruleService, IEventProviderServiceReader eventProviderServiceReader)
{
    protected readonly EventIdsByProvider EventIdsByProvider = ruleService.GetEventIdsByProvider();
    public abstract Task ConsumeAsync();
    public abstract IEnumerable<IEventLogPipeline<WinEvent>> GetUserPipelines();
    public virtual IEnumerable<IEventLogPipeline<KernelData>> GetKernelPipelines() => [];
    protected bool TryGetProviderName(Guid providerGuid, [MaybeNullWhen(false)] out string providerName) => eventProviderServiceReader.TryGetProviderName(providerGuid, out providerName);
    protected bool TryGetManifestedChannelName(Guid providerGuid, int eventId, [MaybeNullWhen(false)] out string channelName) => eventProviderServiceReader.TryGetManifestChannelName(providerGuid, eventId, out channelName);
    protected bool TryGetManifestChannelNames(Guid providerGuid, [MaybeNullWhen(false)] out ISet<string> channelNames) => eventProviderServiceReader.TryGetManifestChannelNames(providerGuid, out channelNames);
    protected bool TryGetMofChannelName(Guid providerGuid, [MaybeNullWhen(false)] out string channelName)
    {
        if (MofChannelResolver.TryGetMofChannelName(providerGuid, out channelName)) return true;
        return eventProviderServiceReader.TryGetMofChannelName(providerGuid, out channelName);
    }

    protected static bool TryGetPollingChannelNames(Guid providerGuid, [MaybeNullWhen(false)] out ISet<string> channelNames)
    {
        return PollingChannelResolver.TryGetPollingChannelNames(providerGuid, out channelNames);
    }

    protected bool TryGetUnknownLengthProperties(Guid providerGuid, int eventId, [MaybeNullWhen(false)] out ISet<string> unknownLengthProperties) => eventProviderServiceReader.TryGetUnknownLengthProperties(providerGuid, eventId, out unknownLengthProperties);
}