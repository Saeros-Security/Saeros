using System.Text.Json.Serialization;

namespace Collector.Detection.Contracts;

[method: JsonConstructor]
public sealed class TargetEventIds(ISet<int> items)
{
    public ISet<int> Items { get; } = items;
}