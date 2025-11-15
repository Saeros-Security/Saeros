using System.Text.Json;
using System.Text.Json.Serialization;
using Shared;

namespace Collector.Core.Converters;

public sealed class ProviderEventIdConverter : JsonConverter<ProviderEventId>
{
    public override ProviderEventId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {	
        var provider = reader.GetString();
        var eventId = reader.GetString();
        return new ProviderEventId(provider, eventId);
    }

    public override void Write(Utf8JsonWriter writer, ProviderEventId value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(ProviderEventId.Provider), value.Provider);
        writer.WriteString(nameof(ProviderEventId.EventId), value.EventId);
        writer.WriteEndObject();
    }

    public override ProviderEventId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()?.Split(':', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return new ProviderEventId(value[0], value[1]);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ProviderEventId value, JsonSerializerOptions options)
    {
        writer.WritePropertyName($"{value.Provider}:{value.EventId}");
    }
}
