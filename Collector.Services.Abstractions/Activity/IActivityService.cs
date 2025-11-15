namespace Collector.Services.Abstractions.Activity;

public interface IActivityService
{
    void SetActive();
    DateTimeOffset LastActive { get; }
}