using System.Text.Json.Serialization;

namespace Collector.Detection.Contracts;

[method: JsonConstructor]
public sealed class ProviderAbbrevations(IDictionary<string, string> items)
{
    public IDictionary<string, string> Items { get; } = items;
}