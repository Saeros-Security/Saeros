using Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;

namespace Collector.Databases.Implementation.Stores.Tracing.Data;

internal sealed class WorkstationData(string domain, string ipAddress, string workstationName, string operatingSystem) : TracingData(nameof(Workstation))
{
    public string Domain { get; } = domain;
    public string IpAddress { get; } = ipAddress;
    public string WorkstationName { get; } = workstationName;
    public string OperatingSystem { get; } = operatingSystem;
}