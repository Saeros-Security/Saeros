using System.Text.Json;
using CommunityToolkit.HighPerformance.Buffers;
using Google.Protobuf;
using Shared;
using Shared.Helpers;

namespace Collector.Services.Implementation.Extensions;

public static class WinEventExtensions
{
    public static ByteString ToJsonWinEvent(this WinEvent winEvent)
    {
        using var arrayBufferWriter = new ArrayPoolBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(arrayBufferWriter);
        JsonSerializer.Serialize(writer, winEvent, SerializationHelper.WinEventJsonTypeInfo);
        return ByteString.CopyFrom(arrayBufferWriter.WrittenSpan);
    }
}