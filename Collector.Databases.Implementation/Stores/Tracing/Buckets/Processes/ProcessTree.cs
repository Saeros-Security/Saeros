using MessagePack;
using MessagePack.Formatters;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;

public readonly struct ProcessTree(string value, string processName)
{
    public string Value { get; } = value;
    public string ProcessName { get; } = processName;

    public ProcessTree() : this(value: string.Empty, processName: string.Empty)
    {
    }
}

internal sealed class ProcessTreeFormatter : IMessagePackFormatter<ProcessTree>
{
    public static readonly IMessagePackFormatter<ProcessTree> Instance = new ProcessTreeFormatter();

    public void Serialize(ref MessagePackWriter writer, ProcessTree value, MessagePackSerializerOptions options)
    {
        writer.CancellationToken.ThrowIfCancellationRequested();
        IFormatterResolver formatterResolver = options.Resolver;
        writer.WriteArrayHeader(2);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Value, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ProcessName, options);
    }

    public ProcessTree Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        try
        {
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var value = string.Empty;
            var processName = string.Empty;
            for (int i = 0; i < length; i++)
            {
                reader.CancellationToken.ThrowIfCancellationRequested();
                switch (i)
                {
                    case 0:
                        value = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        processName = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new ProcessTree(value, processName);
        }
        finally
        {
            reader.Depth--;
        }
    }
}