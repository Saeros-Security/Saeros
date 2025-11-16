namespace Collector.Core.SystemAudits;

public readonly struct SystemAuditKey(SystemAuditType systemAuditType, string? details = null) : IEquatable<SystemAuditKey>
{
    public SystemAuditType SystemAuditType { get; } = systemAuditType;
    public string Details { get; } = details ?? string.Empty;

    public bool Equals(SystemAuditKey other)
    {
        return SystemAuditType == other.SystemAuditType && Details == other.Details;
    }

    public override bool Equals(object? obj)
    {
        return obj is SystemAuditKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)SystemAuditType, Details);
    }
}