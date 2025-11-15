namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;

public readonly struct ProcessKey(string workstationName, string domain, long processId, string processName, long logonId)
    : IEquatable<ProcessKey>
{
    public string WorkstationName { get; } = workstationName;
    public string Domain { get; } = domain;
    public long ProcessId { get; } = processId;
    public string ProcessName { get; } = processName;
    public long LogonId { get; } = logonId;

    public bool Equals(ProcessKey other)
    {
        return WorkstationName.Equals(other.WorkstationName, StringComparison.OrdinalIgnoreCase) && Domain.Equals(other.Domain, StringComparison.OrdinalIgnoreCase) && ProcessId == other.ProcessId && ProcessName.Equals(other.ProcessName, StringComparison.OrdinalIgnoreCase) && LogonId == other.LogonId;
    }

    public override bool Equals(object? obj)
    {
        return obj is ProcessKey other && Equals(other);
    }
    
    public static bool operator ==(ProcessKey left, ProcessKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ProcessKey left, ProcessKey right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        var hashcode = new HashCode();
        hashcode.Add(WorkstationName, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(Domain, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(ProcessId);
        hashcode.Add(ProcessName, StringComparer.OrdinalIgnoreCase);
        hashcode.Add(LogonId);
        return hashcode.ToHashCode();
    }
}