using Collector.Databases.Implementation.Caching.Series.Formatters;
using Collector.Databases.Implementation.Caching.Series.Keys;
using Collector.Databases.Implementation.Caching.Series.Serializers;
using Collector.Databases.Implementation.Caching.Series.Values;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Collector.Databases.Implementation.Caching.Series.Resolvers;

internal sealed class SeriesMessagePackResolver : IFormatterResolver
{
    public static readonly SeriesMessagePackResolver Instance = new();

    private static readonly IFormatterResolver[] Resolvers =
    {
        BuiltinResolver.Instance,
        CompositeResolver.Create(NetworkKeySerializer.Instance, NetworkValueSerializer.Instance, MessagePackSetFormatter<string>.Instance, MessagePackDictionaryFormatter<int, long>.Instance, MessagePackDictionaryFormatter<string, DateTimeOffset>.Instance, MessagePackDictionaryFormatter<NetworkKey, NetworkValue>.Instance)
    };

    private SeriesMessagePackResolver()
    {
        Options = BuildOptions();
    }

    private MessagePackSerializerOptions BuildOptions()
    {
        return MessagePackSerializerOptions.Standard.WithResolver(this).WithCompression(MessagePackCompression.Lz4BlockArray);
    }
    
    public IMessagePackFormatter<T>? GetFormatter<T>()
    {
        return Cache<T>.Formatter;
    }

    public MessagePackSerializerOptions Options { get; }

    private static class Cache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter;

        static Cache()
        {
            foreach (IFormatterResolver item in Resolvers)
            {
                IMessagePackFormatter<T>? f = item.GetFormatter<T>();
                if (f != null)
                {
                    Formatter = f;
                    return;
                }
            }
        }
    }
}