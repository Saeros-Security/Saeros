namespace Collector.Detection.Rules;

public abstract class RuleBase(RuleMetadata metadata) : IEquatable<RuleBase>
{
    public RuleMetadata Metadata { get; } = metadata;
    public string Id { get; } = metadata.Id;

    public bool Equals(RuleBase? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is RuleBase other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}