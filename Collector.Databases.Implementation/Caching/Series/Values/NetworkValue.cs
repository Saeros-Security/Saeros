namespace Collector.Databases.Implementation.Caching.Series.Values;

public struct NetworkValue(string id, long outbound, ISet<string> countries, DateTimeOffset expiration)
    : IEquatable<NetworkValue>
{
    public string Id { get; } = id;
    public long Outbound { get; set; } = outbound;
    public ISet<string> Countries { get; } = countries;

    public DateTimeOffset Expiration = expiration;

    public TimeSpan ExpireIn => Expiration - DateTimeOffset.UtcNow;

    public override bool Equals(object? obj)
    {
        return obj is NetworkValue value && Equals(value);
    }

    public bool Equals(NetworkValue other)
    {
        return Expiration == other.Expiration && Id.Equals(other.Id, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        // IsExpired depends on the current time. Including it in the
        // hash code would cause different hashes for the same value
        // depending on when GetHashCode is called.
        var hashCode = new HashCode();
        hashCode.Add(Expiration);
        hashCode.Add(Id, StringComparer.Ordinal);
        return hashCode.ToHashCode();
    }

    public bool SlideExpiration(TimeSpan timeSpan)
    {
        var newExpiration = DateTimeOffset.UtcNow.Add(timeSpan);
        if (newExpiration <= Expiration)
            return false;
        Expiration = newExpiration;
        return true;
    }

    public static bool operator ==(NetworkValue left, NetworkValue right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NetworkValue left, NetworkValue right)
    {
        return !(left == right);
    }
}