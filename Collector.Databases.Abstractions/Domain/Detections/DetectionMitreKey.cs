namespace Collector.Databases.Abstractions.Domain.Detections;

public sealed class DetectionMitreKey(string ruleId, string level, string mitreId, string tactic, string technique, string subTechnique, string computer) : IEquatable<DetectionMitreKey>
{
    public string RuleId { get; } = ruleId;
    public string Level { get; } = level;
    public string MitreId { get; } = mitreId;
    public string Tactic { get; } = tactic;
    public string Technique { get; } = technique;
    public string SubTechnique { get; } = subTechnique;
    public string Computer { get; } = computer;

    public bool Equals(DetectionMitreKey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return RuleId == other.RuleId && Level == other.Level && MitreId == other.MitreId && Tactic == other.Tactic && Technique == other.Technique && SubTechnique == other.SubTechnique && Computer == other.Computer;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is DetectionMitreKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RuleId, Level, MitreId, Tactic, Technique, SubTechnique, Computer);
    }
}