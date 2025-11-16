using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Collector.Databases.Implementation.Stores.Tracing.Resolvers;

internal sealed class TracingMessagePackResolver : IFormatterResolver
{
    public static readonly TracingMessagePackResolver Instance = new();

    private static readonly IFormatterResolver[] Resolvers =
    {
        BuiltinResolver.Instance,
        CompositeResolver.Create(ProcessFormatter.Instance, UserFormatter.Instance, WorkstationFormatter.Instance, ProcessTreeFormatter.Instance)
    };

    private TracingMessagePackResolver()
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