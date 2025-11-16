using MessagePack;
using MessagePack.Formatters;

namespace Collector.Databases.Implementation.Caching.Series.Formatters;

public sealed class MessagePackDictionaryFormatter<TKey, TValue> : IMessagePackFormatter<Dictionary<TKey, TValue>?> where TKey : notnull
{
    public static readonly IMessagePackFormatter<Dictionary<TKey, TValue>?> Instance = new MessagePackDictionaryFormatter<TKey, TValue>();

    public void Serialize(ref MessagePackWriter writer, Dictionary<TKey, TValue>? value, MessagePackSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNil();
            return;
        }

        var keyFormatter = options.Resolver.GetFormatterWithVerify<TKey>();
        IMessagePackFormatter<TValue> formatter = options.Resolver.GetFormatterWithVerify<TValue>();

        writer.WriteMapHeader(value.Count);
        foreach (var item in value)
        {
            writer.CancellationToken.ThrowIfCancellationRequested();
            keyFormatter.Serialize(ref writer, item.Key, options);
            formatter.Serialize(ref writer, item.Value, options);
        }
    }

    public Dictionary<TKey, TValue>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();
        int count = reader.ReadMapHeader();
        if (count > 0)
        {
            IFormatterResolver resolver = options.Resolver;
            IMessagePackFormatter<TKey> keyFormatter = resolver.GetFormatterWithVerify<TKey>();
            IMessagePackFormatter<TValue> formatter = options.Resolver.GetFormatterWithVerify<TValue>();
            IDictionary<TKey, TValue> dictionary = result;

            options.Security.DepthStep(ref reader);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    reader.CancellationToken.ThrowIfCancellationRequested();
                    var key = keyFormatter.Deserialize(ref reader, options);
                    var value = formatter.Deserialize(ref reader, options);
                    dictionary.Add(key, value);
                }
            }
            finally
            {
                reader.Depth--;
            }
        }

        return result;
    }
}