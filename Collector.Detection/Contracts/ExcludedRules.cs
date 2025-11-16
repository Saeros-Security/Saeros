using System.Text.Json.Serialization;

namespace Collector.Detection.Contracts;

[method: JsonConstructor]
public sealed class ExcludedRules(ISet<string> items)
{
    public ISet<string> Items { get; } = items;
}