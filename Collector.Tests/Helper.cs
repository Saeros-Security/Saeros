using System.Diagnostics.CodeAnalysis;
using Collector.Detection.Rules;
using Collector.Detection.Rules.Helpers;
using Collector.Detection.Rules.Serializers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Collector.Tests;

internal static class Helper
{
    public static bool TryGetRule(string yamlRule, [MaybeNullWhen(false)] out RuleBase rule, [MaybeNullWhen(false)] out ISet<string> properties, [MaybeNullWhen(true)] out string error)
    {
        var aliases = ConfigHelper.GetAliases();
        var details = ConfigHelper.GetDetails();
        var channels = ConfigHelper.GetChannelAbbreviations();
        var providers = ConfigHelper.GetProviderAbbreviations();
        return RuleSerializer.TryDeserialize(NullLogger.Instance, yamlRule, _ => true, aliases, details, channels, providers, domainControllers: new HashSet<string>(), out rule, out _, out _, out properties, out error);
    }
}