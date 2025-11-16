using Shared.Models.Detections;

namespace Collector.Databases.Abstractions.Domain.Rules;

public readonly struct RuleCountAndSeverity(string ruleTitle, DetectionSeverity severity, long count) : IEquatable<RuleCountAndSeverity>
{
    public string RuleTitle { get; } = ruleTitle;
    public DetectionSeverity Severity { get; } = severity;
    public long Count { get; } = count;

    public bool Equals(RuleCountAndSeverity other)
    {
        return RuleTitle == other.RuleTitle;
    }

    public override bool Equals(object? obj)
    {
        return obj is RuleCountAndSeverity other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RuleTitle);
    }
    public static bool operator ==(RuleCountAndSeverity left, RuleCountAndSeverity right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RuleCountAndSeverity left, RuleCountAndSeverity right)
    {
        return !(left == right);
    }
}