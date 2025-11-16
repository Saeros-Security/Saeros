namespace Collector.Databases.Abstractions.Caching.LRU;

public readonly struct DetectionKey(string ruleId, string computer, string detailsHash) : IEquatable<DetectionKey>
{
    private string RuleId { get; } = ruleId;
    private string Computer { get; } = computer;
    private string DetailsHash { get; } = detailsHash;

    public bool Equals(DetectionKey other)
    {
        return RuleId == other.RuleId && Computer == other.Computer && DetailsHash == other.DetailsHash;
    }

    public override bool Equals(object? obj)
    {
        return obj is DetectionKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RuleId, Computer, DetailsHash);
    }
}