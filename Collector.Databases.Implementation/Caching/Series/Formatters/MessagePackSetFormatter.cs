using MessagePack;
using MessagePack.Formatters;

namespace Collector.Databases.Implementation.Caching.Series.Formatters;

public sealed class MessagePackSetFormatter<T> : IMessagePackFormatter<ISet<T>?>
{
    public static readonly IMessagePackFormatter<ISet<T>?> Instance = new MessagePackSetFormatter<T>();

    public void Serialize(ref MessagePackWriter writer, ISet<T>? value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
        }
        else
        {
            IMessagePackFormatter<T> formatter = options.Resolver.GetFormatterWithVerify<T>();

            var c = value.Count;
            writer.WriteArrayHeader(c);

            foreach (var item in value)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                formatter.Serialize(ref writer, item, options);
            }
        }
    }

    public ISet<T>? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return default;
        }
        else
        {
            IMessagePackFormatter<T> formatter = options.Resolver.GetFormatterWithVerify<T>();

            var len = reader.ReadArrayHeader();
            var set = new HashSet<T>();
            options.Security.DepthStep(ref reader);
            try
            {
                for (int i = 0; i < len; i++)
                {
                    reader.CancellationToken.ThrowIfCancellationRequested();
                    set.Add(formatter.Deserialize(ref reader, options));
                }
            }
            finally
            {
                reader.Depth--;
            }

            return set;
        }
    }
}