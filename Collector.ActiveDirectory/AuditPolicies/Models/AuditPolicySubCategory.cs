namespace Collector.ActiveDirectory.AuditPolicies.Models;

public sealed class AuditPolicySubCategory(Guid subCategoryGuid) : IEquatable<AuditPolicySubCategory>
{
    public Guid SubCategoryGuid { get; } = subCategoryGuid;

    public bool Equals(AuditPolicySubCategory? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return SubCategoryGuid.Equals(other.SubCategoryGuid);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is AuditPolicySubCategory other && Equals(other);
    }

    public override int GetHashCode()
    {
        return SubCategoryGuid.GetHashCode();
    }
}