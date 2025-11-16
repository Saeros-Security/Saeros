using Collector.Databases.Implementation.Caching.Series.Values;
using MessagePack;
using MessagePack.Formatters;

namespace Collector.Databases.Implementation.Caching.Series.Serializers;

internal sealed class NetworkValueSerializer : IMessagePackFormatter<NetworkValue>
{
    public static readonly IMessagePackFormatter<NetworkValue> Instance = new NetworkValueSerializer();

    public void Serialize(ref MessagePackWriter writer, NetworkValue value, MessagePackSerializerOptions options)
    {
        writer.CancellationToken.ThrowIfCancellationRequested();
        IFormatterResolver formatterResolver = options.Resolver;
        writer.WriteArrayHeader(4);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Id, options);
        formatterResolver.GetFormatterWithVerify<long>().Serialize(ref writer, value.Outbound, options);
        formatterResolver.GetFormatterWithVerify<ISet<string>>().Serialize(ref writer, value.Countries, options);
        formatterResolver.GetFormatterWithVerify<DateTimeOffset>().Serialize(ref writer, value.Expiration, options);
    }

    public NetworkValue Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        try
        {
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var id = string.Empty;
            var outbound = 0L;
            ISet<string>? countries = null;
            var expiration = DateTimeOffset.MinValue;
            for (int i = 0; i < length; i++)
            {
                reader.CancellationToken.ThrowIfCancellationRequested();
                switch (i)
                {
                    case 0:
                        id = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        outbound = formatterResolver.GetFormatterWithVerify<long>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        countries = formatterResolver.GetFormatterWithVerify<ISet<string>>().Deserialize(ref reader, options);
                        break;
                    case 3:
                        expiration = formatterResolver.GetFormatterWithVerify<DateTimeOffset>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new NetworkValue(id, outbound, countries ?? new HashSet<string>(), expiration);
        }
        finally
        {
            reader.Depth--;
        }
    }
}