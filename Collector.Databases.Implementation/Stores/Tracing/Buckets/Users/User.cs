using MessagePack;
using MessagePack.Formatters;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;

internal readonly struct User(string name, string sid, string domain, long logonId, int logonType, bool privileged, string sourceIp, string sourceHostname, string logon, DateTimeOffset date) : IEquatable<User>
{
    public string Name { get; } = name;
    public string Sid { get; } = sid;
    public string Domain { get; } = domain;
    public long LogonId { get; } = logonId;
    public int LogonType { get; } = logonType;
    public bool Privileged { get; } = privileged;
    public string SourceIp { get; } = sourceIp;
    public string SourceHostname { get; } = sourceHostname;
    public string Logon { get; } = logon;
    public DateTimeOffset Date { get; } = date;

    public bool Equals(User other)
    {
        return Domain.Equals(other.Domain, StringComparison.OrdinalIgnoreCase) && Sid.Equals(other.Sid, StringComparison.OrdinalIgnoreCase) && LogonId == other.LogonId;
    }

    public override bool Equals(object? obj)
    {
        return obj is User other && Equals(other);
    }

    public static bool operator ==(User left, User right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(User left, User right)
    {
        return !(left == right);
    }
    
    public override int GetHashCode()
    {
        var hashcode = new HashCode();
        hashcode.Add(Domain, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(Sid, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(LogonId);
        return hashcode.ToHashCode();
    }
}

internal sealed class UserFormatter : IMessagePackFormatter<User>
{
    public static readonly IMessagePackFormatter<User> Instance = new UserFormatter();

    public void Serialize(ref MessagePackWriter writer, User value, MessagePackSerializerOptions options)
    {
        writer.CancellationToken.ThrowIfCancellationRequested();
        IFormatterResolver formatterResolver = options.Resolver;
        writer.WriteArrayHeader(10);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Sid, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Domain, options);
        formatterResolver.GetFormatterWithVerify<long>().Serialize(ref writer, value.LogonId, options);
        formatterResolver.GetFormatterWithVerify<int>().Serialize(ref writer, value.LogonType, options);
        formatterResolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.Privileged, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.SourceIp, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.SourceHostname, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Logon, options);
        formatterResolver.GetFormatterWithVerify<long>().Serialize(ref writer, value.Date.Ticks, options);
    }

    public User Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        try
        {
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var name = string.Empty;
            var sid = string.Empty;
            var domain = string.Empty;
            long logonId = 0L;
            int logonType = 0;
            bool privileged = false;
            string ipAddress = string.Empty;
            string sourceHostname = string.Empty;
            string logon = string.Empty;
            var ticks = DateTimeOffset.MinValue.Ticks;
            for (int i = 0; i < length; i++)
            {
                reader.CancellationToken.ThrowIfCancellationRequested();
                switch (i)
                {
                    case 0:
                        name = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        sid = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        domain = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 3:
                        logonId = formatterResolver.GetFormatterWithVerify<long>().Deserialize(ref reader, options);
                        break;
                    case 4:
                        logonType = formatterResolver.GetFormatterWithVerify<int>().Deserialize(ref reader, options);
                        break;
                    case 5:
                        privileged = formatterResolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                        break;
                    case 6:
                        ipAddress = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 7:
                        sourceHostname = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 8:
                        logon = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 9:
                        ticks = formatterResolver.GetFormatterWithVerify<long>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new User(name, sid, domain, logonId, logonType, privileged, ipAddress, sourceHostname, logon, new DateTimeOffset(ticks, TimeSpan.Zero));
        }
        finally
        {
            reader.Depth--;
        }
    }
}