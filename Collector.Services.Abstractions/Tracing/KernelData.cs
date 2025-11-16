namespace Collector.Services.Abstractions.Tracing;

public abstract class KernelData(string computer)
{
    public string Computer { get; } = computer;
}