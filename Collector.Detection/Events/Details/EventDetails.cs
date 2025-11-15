using System.Text.Json.Serialization;
using Collector.Detection.Contracts;
using Collector.Detection.Rules.Helpers;

namespace Collector.Detection.Events.Details;

[method: JsonConstructor]
internal sealed class EventDetails(PropertyMappings propertyMappings, EventTitles eventTitles, Contracts.Details details)
{
    public PropertyMappings PropertyMappings { get; } = propertyMappings;
    public EventTitles EventTitles { get; } = eventTitles;
    public Contracts.Details Details { get; } = details;

    private static readonly Lazy<EventDetails> _instance = new(() => new EventDetails(ConfigHelper.GetPropertyMappings(), ConfigHelper.GetEventTitles(), ConfigHelper.GetDetails()), LazyThreadSafetyMode.ExecutionAndPublication);
    internal static readonly EventDetails Instance = _instance.Value;
}