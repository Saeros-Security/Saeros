using Collector.Services.Abstractions.Activity;

namespace Collector.Services.Implementation.Bridge.Activity;

public sealed class ActivityService : IActivityService
{
    public void SetActive()
    {
        LastActive = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset LastActive { get; private set; }
}