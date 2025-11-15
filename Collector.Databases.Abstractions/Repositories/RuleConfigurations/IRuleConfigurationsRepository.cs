using System.Diagnostics.CodeAnalysis;
using Collector.Detection.Contracts;

namespace Collector.Databases.Abstractions.Repositories.RuleConfigurations;

public interface IRuleConfigurationsRepository
{
    void AddAliases(Aliases aliases);
    void AddEventTitles(EventTitles eventTitles);
    void AddPropertyMappings(PropertyMappings propertyMappings);
    void AddDetails(Details details);
    void AddChannelAbbrevations(ChannelAbbrevations channelAbbrevations);
    void AddProviderAbbrevations(ProviderAbbrevations providerAbbrevations);
    void AddExcludedRules(ExcludedRules excludedRules);
    void AddNoisyRules(NoisyRules noisyRules);
    void AddProvenRules(ProvenRules provenRules);
    void AddTargetEventIds(TargetEventIds targetEventIds);
    bool TryGetAliases([MaybeNullWhen(false)] out Aliases aliases);
    bool TryGetEventTitles([MaybeNullWhen(false)] out EventTitles eventTitles);
    bool TryGetPropertyMappings([MaybeNullWhen(false)] out PropertyMappings propertyMappings);
    bool TryGetDetails([MaybeNullWhen(false)] out Details details);
    bool TryGetChannelAbbrevations([MaybeNullWhen(false)] out ChannelAbbrevations channelAbbrevations);
    bool TryGetProviderAbbrevations([MaybeNullWhen(false)] out ProviderAbbrevations providerAbbrevations);
    bool TryGetExcludedRules([MaybeNullWhen(false)] out ExcludedRules excludedRules);
    bool TryGetNoisyRules([MaybeNullWhen(false)] out NoisyRules noisyRules);
    bool TryGetProvenRules([MaybeNullWhen(false)] out ProvenRules provenRules);
    bool TryGetTargetEventIds([MaybeNullWhen(false)] out TargetEventIds targetEventIds);
}