namespace Collector.Services.Abstractions.Processes;

public interface IProcessTreeService : IAsyncDisposable
{
    void Add(string workstationName, string domain, Guid providerGuid, uint eventId, IDictionary<string, string> eventData);
}