using MessagePack;
using MessagePack.Formatters;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;

internal readonly struct Workstation(string workstationName, string ipAddress, string domain, DateTimeOffset date) : IEquatable<Workstation>
{
    public string WorkstationName { get; } = workstationName;
    public string IpAddress { get; } = ipAddress;
    public string Domain { get; } = domain;
    public DateTimeOffset Date { get; } = date;
    
    public bool Equals(Workstation other)
    {
        return Domain.Equals(other.Domain, StringComparison.OrdinalIgnoreCase) && WorkstationName.Equals(other.WorkstationName, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is Workstation other && Equals(other);
    }
    
    public static bool operator ==(Workstation left, Workstation right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Workstation left, Workstation right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        var hashcode = new HashCode();
        hashcode.Add(Domain, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(WorkstationName, StringComparer.OrdinalIgnoreCase);
        return hashcode.ToHashCode();
    }
}

internal sealed class WorkstationFormatter : IMessagePackFormatter<Workstation>
{
    public static readonly IMessagePackFormatter<Workstation> Instance = new WorkstationFormatter();

    public void Serialize(ref MessagePackWriter writer, Workstation value, MessagePackSerializerOptions options)
    {
        writer.CancellationToken.ThrowIfCancellationRequested();
        IFormatterResolver formatterResolver = options.Resolver;
        writer.WriteArrayHeader(4);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.WorkstationName, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.IpAddress, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Domain, options);
        formatterResolver.GetFormatterWithVerify<long>().Serialize(ref writer, value.Date.Ticks, options);
    }

    public Workstation Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        try
        {
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var workstationName = string.Empty;
            var ipAddress = string.Empty;
            var domain = string.Empty;
            var ticks = DateTimeOffset.MinValue.Ticks;
            for (int i = 0; i < length; i++)
            {
                reader.CancellationToken.ThrowIfCancellationRequested();
                switch (i)
                {
                    case 0:
                        workstationName = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        ipAddress = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        domain = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 3:
                        ticks = formatterResolver.GetFormatterWithVerify<long>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new Workstation(workstationName, ipAddress, domain, new DateTimeOffset(ticks, TimeSpan.Zero));
        }
        finally
        {
            reader.Depth--;
        }
    }
}