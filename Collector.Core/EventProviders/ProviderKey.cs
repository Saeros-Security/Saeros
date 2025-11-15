namespace Collector.Core.EventProviders;

public readonly struct ProviderKey(Guid providerGuid, string providerName, ProviderType providerType, bool userTrace = true) : IEquatable<ProviderKey>
{
    public Guid ProviderGuid { get; } = providerGuid;
    public string ProviderName { get; } = providerName;
    public ProviderType ProviderType { get; } = providerType;
    public bool UserTrace { get; } = userTrace;

    public bool Equals(ProviderKey other)
    {
        return ProviderGuid.Equals(other.ProviderGuid);
    }

    public override bool Equals(object? obj)
    {
        return obj is ProviderKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ProviderGuid);
    }
}