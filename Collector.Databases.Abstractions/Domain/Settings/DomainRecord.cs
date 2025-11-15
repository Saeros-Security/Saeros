namespace Collector.Databases.Abstractions.Domain.Settings;

public sealed record DomainRecord(string Name, string PrimaryDomainController, int DomainControllerCount, bool ShouldUpdate);