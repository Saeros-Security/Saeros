using System.Text.Json.Serialization;
using Shared;

namespace Collector.Detection.Contracts;

[method: JsonConstructor]
public sealed class Details(IDictionary<ProviderEventId, string> items)
{
    public IDictionary<ProviderEventId, string> Items { get; } = items;
}