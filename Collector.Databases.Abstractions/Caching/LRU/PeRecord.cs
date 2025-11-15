namespace Collector.Databases.Abstractions.Caching.LRU;

public sealed record PeRecord(string ImpHash, string Version, string Description, string Product, string Company, string OriginalFilename);