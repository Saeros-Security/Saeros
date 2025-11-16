using System.Diagnostics.CodeAnalysis;
using Collector.Detection.Contracts;
using Collector.Detection.Rules.Builders;
using Collector.Detection.Rules.Extensions;
using Detection.Yaml;
using Microsoft.Extensions.Logging;
using Shared;
using YamlDotNet.Core;

namespace Collector.Detection.Rules.Serializers;

public static class RuleSerializer
{
    public static bool TryDeserialize(ILogger logger, string yamlString, Predicate<RuleMetadata> filter, Aliases aliases, Details details, ChannelAbbrevations channelAbbreviations, ProviderAbbrevations providerAbbreviations, ISet<string> domainControllers, [MaybeNullWhen(false)] out RuleBase rule, [MaybeNullWhen(false)] out ISet<ChannelEventId> channelEventIds, [MaybeNullWhen(false)] out ISet<ProviderEventId> providerEventIds, [MaybeNullWhen(false)] out ISet<string> properties, [MaybeNullWhen(true)] out string error)
    {
        rule = null;
        error = null;
        channelEventIds = null;
        providerEventIds = null;
        properties = null;
        var ruleMetadata = new RuleMetadata();
        try
        {
            var yamlRules = YamlParser.DeserializeMany<YamlRule>(yamlString).ToList();
            ruleMetadata = yamlRules.ToMetadata();
            if (!filter(ruleMetadata))
            {
                error = "The rule has been filtered";
                return false;
            }
            
            rule = RuleBuilder.Build(yamlRules, ruleMetadata, aliases, details, channelAbbreviations, providerAbbreviations, domainControllers, out channelEventIds, out providerEventIds, out properties);
            return true;
        }
        catch (Exception ex)
        {
            if (ex is Parlot.ParseException parseException)
            {
                error = $"[{parseException.Source} {parseException.Position}]: {parseException.Message}";
            }
            else if (ex is YamlException yamlException)
            {
                error = yamlException.ToString();
            }
            else
            {
                error = ex.Message;
            }
            
            logger.LogError(ex, "An error has occurred while deserializing the rule {Rule}: {Message}", ruleMetadata.Id, ex.Message);
            return false;
        }
    }
}