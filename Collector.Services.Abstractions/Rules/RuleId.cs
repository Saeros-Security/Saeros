namespace Collector.Services.Abstractions.Rules;

public readonly struct RuleId(string id, bool useSysmon = false) : IEquatable<RuleId>
{
    public string Id { get; } = id;
    public bool UseSysmon { get; } = useSysmon;

    public bool Equals(RuleId other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is RuleId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}