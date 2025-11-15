using Collector.Databases.Abstractions.Caching.LRU;

namespace Collector.Services.Abstractions.Processes;

public interface IPeService
{
    PeRecord Scan(string processPath);
}