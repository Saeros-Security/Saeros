using CommunityToolkit.HighPerformance.Buffers;
using Google.Protobuf;
using System.Text.Json;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Extensions;

namespace Collector.Databases.Implementation.Extensions;

internal static class TracerExtensions
{
    public static ByteString FromTracer<T>(this T tracer) where T : Tracer
    {
        using var arrayBufferWriter = new ArrayPoolBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(arrayBufferWriter);
        JsonSerializer.Serialize(writer, tracer, TracerJsonExtensions.Options);
        return ByteString.CopyFrom(arrayBufferWriter.WrittenSpan);
    }
}
