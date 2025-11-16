using System.Text.Json.Serialization;
using Collector.Detection.Rules.Helpers;

namespace Collector.Detection.Contracts;

[method: JsonConstructor]
public sealed class Aliases(IDictionary<string, string> items)
{
    public IDictionary<string, string> Items { get; } = items;

    private static readonly Lazy<Aliases> _instance = new(ConfigHelper.GetAliases, LazyThreadSafetyMode.ExecutionAndPublication);
    internal static readonly Aliases Instance = _instance.Value;
}