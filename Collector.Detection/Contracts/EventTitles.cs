using System.Text.Json.Serialization;
using Shared;

namespace Collector.Detection.Contracts;

[method: JsonConstructor]
public sealed class EventTitles(IDictionary<ChannelEventId, string> items)
{
    public IDictionary<ChannelEventId, string> Items { get; } = items;
}