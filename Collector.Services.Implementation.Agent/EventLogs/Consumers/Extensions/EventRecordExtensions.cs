using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.Parsers;
using Collector.Services.Implementation.Agent.EventLogs.Extensions;
using Shared;
using Shared.Extensions;
using TurboXml;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.Extensions;

internal static class EventRecordExtensions
{
    private readonly struct EventRecordKey(int eventId, Guid providerGuid, byte version) : IEquatable<EventRecordKey>
    {
        private int EventId { get; } = eventId;
        private Guid ProviderGuid { get; } = providerGuid;
        private byte Version { get; } = version;

        public bool Equals(EventRecordKey other)
        {
            return EventId == other.EventId && ProviderGuid.Equals(other.ProviderGuid) && Version == other.Version;
        }

        public override bool Equals(object? obj)
        {
            return obj is EventRecordKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EventId, ProviderGuid, Version);
        }
    }

    private sealed record Property(string PropertyName, bool IsHex);
    private static readonly ConcurrentDictionary<EventRecordKey, Property[]> PropertiesByEventKey = new();

    public static bool TryGetWinEvent(this EventRecord record, [MaybeNullWhen(false)] out WinEvent winEvent, string? server = null)
    {
        winEvent = null;
        if (record.RecordId is null || record.ProviderId == null || record.Version == null) return false;
        var key = new EventRecordKey(record.Id, record.ProviderId.Value, record.Version.Value);
        if (PropertiesByEventKey.TryGetValue(key, out var properties))
        {
            winEvent = record.BuildWinEvent(record.LogName, properties.Length, out var data);
            for (var i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                var property = record.Properties[i];
                var value = property?.Value?.ToString();
                if (value is null) break;
                if (prop.IsHex)
                {
                    if (short.TryParse(value, out var shortValue))
                    {
                        data.Add(prop.PropertyName, shortValue.ToString("x"));
                    }
                    else if (int.TryParse(value, out var intValue))
                    {
                        data.Add(prop.PropertyName, intValue.ToString("x"));
                    }
                    else if (long.TryParse(value, out var longValue))
                    {
                        data.Add(prop.PropertyName, longValue.ToString("x"));
                    }
                }
                else
                {
                    data.Add(prop.PropertyName, value);
                }
            }
        }
        else
        {
            var xml = record.ToXml();
            var parser = new EventLogXmlParser();
            XmlParser.Parse(xml, ref parser);
            winEvent = record.BuildWinEvent(record.LogName, parser.Properties.Count, out var data);
            foreach (var pair in parser.Properties)
            {
                data.Add(pair.Key, pair.Value);
            }

            PropertiesByEventKey.TryAdd(key, parser.Properties.Select(kvp => kvp.Key).Select(propertyKey =>
            {
                if (data.TryGetValue(propertyKey, out var value))
                {
                    if (value.StartsWith("0x"))
                    {
                        return new Property(propertyKey, IsHex: true);
                    }
                }
                
                return new Property(propertyKey, IsHex: false);
            }).ToArray());
        }

        if (server is not null)
        {
            winEvent.System[WinEventExtensions.ComputerKey] = server;
        }
        
        return true;
    }
}