namespace Collector.ActiveDirectory.AuditPolicies.Models;

public sealed class AuditPolicyCategory(Guid categoryGuid, HashSet<AuditPolicySubCategory> subCategories) : IEquatable<AuditPolicyCategory>
{
    public Guid CategoryGuid { get; } = categoryGuid;
    public HashSet<AuditPolicySubCategory> SubCategories { get; } = subCategories;

    public bool Equals(AuditPolicyCategory? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CategoryGuid.Equals(other.CategoryGuid);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is AuditPolicyCategory other && Equals(other);
    }

    public override int GetHashCode()
    {
        return CategoryGuid.GetHashCode();
    }
}