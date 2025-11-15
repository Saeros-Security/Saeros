using System.Text.Json.Serialization;
using Collector.Detection.Rules.Mappings;
using Shared;

namespace Collector.Detection.Contracts;

[method: JsonConstructor]
public sealed class PropertyMappings(IDictionary<ChannelEventId, PropertyMapping> items)
{
    public IDictionary<ChannelEventId, PropertyMapping> Items { get; } = items;
}