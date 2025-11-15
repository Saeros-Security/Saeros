using Collector.Core.EventProviders;

namespace Collector.Services.Implementation.Agent.EventProviders.Manifests;

internal sealed class ProviderManifest(string providerName, ProviderType providerType, Guid providerGuid) : IEquatable<ProviderManifest>
{
    public string ProviderName { get; } = providerName;
    public ProviderType ProviderType { get; } = providerType;
    public Guid ProviderGuid { get; } = providerGuid;

    public bool Equals(ProviderManifest? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ProviderName == other.ProviderName && ProviderType == other.ProviderType && ProviderGuid == other.ProviderGuid;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is ProviderManifest other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ProviderName, ProviderType, ProviderGuid);
    }
}