using Collector.Databases.Implementation.Caching.Series.Keys;
using MessagePack;
using MessagePack.Formatters;

namespace Collector.Databases.Implementation.Caching.Series.Serializers;

internal sealed class NetworkKeySerializer : IMessagePackFormatter<NetworkKey>
{
    public static readonly IMessagePackFormatter<NetworkKey> Instance = new NetworkKeySerializer();

    public void Serialize(ref MessagePackWriter writer, NetworkKey value, MessagePackSerializerOptions options)
    {
        writer.CancellationToken.ThrowIfCancellationRequested();
        IFormatterResolver formatterResolver = options.Resolver;
        writer.WriteArrayHeader(2);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Computer, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ProcessName, options);
    }

    public NetworkKey Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        try
        {
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var computer = string.Empty;
            var processName = string.Empty;
            for (int i = 0; i < length; i++)
            {
                reader.CancellationToken.ThrowIfCancellationRequested();
                switch (i)
                {
                    case 0:
                        computer = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        processName = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new NetworkKey(computer, processName);
        }
        finally
        {
            reader.Depth--;
        }
    }
}