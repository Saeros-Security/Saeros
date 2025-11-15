using System.Text.Json.Serialization;
using Collector.Databases.Implementation.Extensions;
using Streaming;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Kernel;

public class NetworkTracer(string computer, string processName, long outbound, ISet<string> countries) : KernelTracer
{
    [JsonPropertyName("Computer")]
    public string Computer { get; } = computer;

    [JsonPropertyName("ProcessName")]
    public string ProcessName { get; } = processName;
    
    [JsonPropertyName("Outbound")]
    public long Outbound { get; } = outbound;
    
    [JsonPropertyName("Countries")]
    public ISet<string> Countries { get; } = countries;

    public override TraceContract ToContract()
    {
        return new TraceContract
        {
            Content = this.FromTracer()
        };
    }
}