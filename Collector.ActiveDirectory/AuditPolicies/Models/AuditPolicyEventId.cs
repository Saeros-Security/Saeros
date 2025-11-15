namespace Collector.ActiveDirectory.AuditPolicies.Models;

public sealed class AuditPolicyEventId(int eventId, AuditPolicyStatus status = AuditPolicyStatus.Success) : IEquatable<AuditPolicyEventId>
{
    public int EventId { get; } = eventId;
    public AuditPolicyStatus Status { get; } = status;

    public bool Equals(AuditPolicyEventId? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EventId == other.EventId;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((AuditPolicyEventId)obj);
    }

    public override int GetHashCode()
    {
        return EventId;
    }
}