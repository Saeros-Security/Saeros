using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Collector.Detection.Converters;

public static class SigmaRuleConverter
{
    public static bool TryConvertSigmaRule(ILogger logger, string sigmaRule, bool sysmonInstalled, [MaybeNullWhen(false)] out string convertedRule, [MaybeNullWhen(true)] out string error)
    {
        convertedRule = null;
        var converter = new LogSourceConverter(logger, sigmaRule);
        if (converter.TryConvert(sysmonInstalled, out convertedRule, out error))
        {
            return true;
        }

        return false;
    }
}