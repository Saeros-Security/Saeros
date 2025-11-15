namespace Collector.Databases.Implementation.Caching.Series.Keys;

public readonly struct NetworkKey(string computer, string processName) : IEquatable<NetworkKey>
{
    public string Computer { get; } = computer;
    public string ProcessName { get; } = processName;

    public override bool Equals(object? obj)
    {
        return obj is NetworkKey value && Equals(value);
    }

    public bool Equals(NetworkKey other)
    {
        return Computer.Equals(other.Computer, StringComparison.Ordinal) && ProcessName.Equals(other.ProcessName, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Computer, StringComparer.Ordinal);
        hashCode.Add(ProcessName, StringComparer.Ordinal);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(NetworkKey left, NetworkKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NetworkKey left, NetworkKey right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return $"{Computer};{ProcessName}";
    }
}