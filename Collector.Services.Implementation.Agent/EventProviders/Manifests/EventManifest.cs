namespace Collector.Services.Implementation.Agent.EventProviders.Manifests;

internal sealed class EventManifest(int eventId, byte version, string channel, ISet<string> unknownLengthProperties) : IEquatable<EventManifest>
{
    public int EventId { get; } = eventId;
    private byte Version { get; } = version;
    public string Channel { get; } = channel;
    public ISet<string> UnknownLengthProperties { get; } = unknownLengthProperties;

    public bool Equals(EventManifest? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EventId == other.EventId && Version == other.Version && Channel == other.Channel;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is EventManifest other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(EventId, Version, Channel);
    }
}