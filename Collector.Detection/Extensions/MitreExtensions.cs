using Collector.Detection.Mitre;
using Streaming;

namespace Collector.Detection.Extensions;

public static class MitreExtensions
{
    public static IEnumerable<T> GetMitre<T>(IEnumerable<string> tags, Func<MitreComponent, T> selector)
    {
        return MitreAttackResolver.GetComponents(tags).Select(selector).Take(1).ToList();
    }

    public static IEnumerable<T> GetMitre<T>(this RuleContract rule, IEnumerable<string> tags, Func<MitreComponent, T> selector)
    {
        return MitreAttackResolver.GetComponents(tags).Select(selector).Take(1).ToList();
    }
}