using MessagePack;
using MessagePack.Formatters;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;

internal readonly struct Process(string processName, string processId, string workstationName, string username, string userSid, long logonId, string commandLine, string parentProcessName, bool elevated, string domain, DateTimeOffset date) : IEquatable<Process>
{
    public string ProcessName { get; } = processName;
    public string ProcessId { get; } = processId;
    public string WorkstationName { get; } = workstationName;
    public string Username { get; } = username;
    public string UserSid { get; } = userSid;
    public long LogonId { get; } = logonId;
    public string CommandLine { get; } = commandLine;
    public string ParentProcessName { get; } = parentProcessName;
    public bool Elevated { get; } = elevated;
    public string Domain { get; } = domain;
    public DateTimeOffset Date { get; } = date;

    public bool Equals(Process other)
    {
        return Domain.Equals(other.Domain, StringComparison.OrdinalIgnoreCase) && WorkstationName.Equals(other.WorkstationName, StringComparison.OrdinalIgnoreCase) && ProcessId.Equals(other.ProcessId, StringComparison.OrdinalIgnoreCase) && ProcessName.Equals(other.ProcessName, StringComparison.OrdinalIgnoreCase) && LogonId == other.LogonId;
    }

    public override bool Equals(object? obj)
    {
        return obj is Process other && Equals(other);
    }
    
    public static bool operator ==(Process left, Process right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Process left, Process right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        var hashcode = new HashCode();
        hashcode.Add(Domain, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(WorkstationName, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(ProcessId, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(ProcessName, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(LogonId);
        return hashcode.ToHashCode();
    }
}

internal sealed class ProcessFormatter : IMessagePackFormatter<Process>
{
    public static readonly IMessagePackFormatter<Process> Instance = new ProcessFormatter();

    public void Serialize(ref MessagePackWriter writer, Process value, MessagePackSerializerOptions options)
    {
        writer.CancellationToken.ThrowIfCancellationRequested();
        IFormatterResolver formatterResolver = options.Resolver;
        writer.WriteArrayHeader(11);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ProcessName, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ProcessId, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.WorkstationName, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Username, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.UserSid, options);
        formatterResolver.GetFormatterWithVerify<long>().Serialize(ref writer, value.LogonId, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.CommandLine, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ParentProcessName, options);
        formatterResolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.Elevated, options);
        formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Domain, options);
        formatterResolver.GetFormatterWithVerify<long>().Serialize(ref writer, value.Date.Ticks, options);
    }

    public Process Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        try
        {
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var processName = string.Empty;
            var processId = string.Empty;
            var workstationName = string.Empty;
            var username = string.Empty;
            var userSid = string.Empty;
            long logonId = 0L;
            var commandLine = string.Empty;
            var parentProcessName = string.Empty;
            var elevated = false;
            var domain = string.Empty;
            var ticks = DateTimeOffset.MinValue.Ticks;
            for (int i = 0; i < length; i++)
            {
                reader.CancellationToken.ThrowIfCancellationRequested();
                switch (i)
                {
                    case 0:
                        processName = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        processId = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        workstationName = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 3:
                        username = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 4:
                        userSid = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 5:
                        logonId = formatterResolver.GetFormatterWithVerify<long>().Deserialize(ref reader, options);
                        break;
                    case 6:
                        commandLine = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 7:
                        parentProcessName = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 8:
                        elevated = formatterResolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
                        break;
                    case 9:
                        domain = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    case 10:
                        ticks = formatterResolver.GetFormatterWithVerify<long>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return new Process(processName, processId, workstationName, username, userSid, logonId, commandLine, parentProcessName, elevated, domain, new DateTimeOffset(ticks, TimeSpan.Zero));
        }
        finally
        {
            reader.Depth--;
        }
    }
}