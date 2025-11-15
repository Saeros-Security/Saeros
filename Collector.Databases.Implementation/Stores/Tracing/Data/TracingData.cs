namespace Collector.Databases.Implementation.Stores.Tracing.Data;

internal abstract class TracingData(string type)
{
    public string Type { get; } = type;
}