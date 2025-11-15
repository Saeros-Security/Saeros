using System.Text.Json.Serialization;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Extensions;
using Shared;
using Streaming;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;

public class ProcessTracer(string domain, string workstationName, DateTime date) : Tracer
{
    private static readonly HashSet<ushort> EventIds = [4688, 4689];

    [JsonPropertyName("Domain")]
    public string Domain { get; } = domain;
    
    [JsonPropertyName("WorkstationName")]
    public string WorkstationName { get; } = workstationName;
    
    [JsonPropertyName("Date")]
    public DateTime Date { get; } = date;

    private static bool Compatible(WinEvent winEvent) => EventIds.Contains(winEvent.EventId);

    public static async ValueTask<Tracer> CreateAsync(WinEvent winEvent, ILogonStore logonStore, Action<uint, string, DateTime> onCreation, Action<uint, string, DateTime> onTermination, CancellationToken cancellationToken)
    {
        if (!Compatible(winEvent)) return DefaultTracer.Instance;
        return winEvent.EventId switch
        {
            4688 => await Process4688.ToProcessAsync(winEvent, logonStore, onCreation, cancellationToken),
            4689 => Process4689.ToProcess(winEvent, onTermination),
            _ => DefaultTracer.Instance
        };
    }

    public override TraceContract ToContract()
    {
        return new TraceContract
        {
            Content = this.FromTracer()
        };
    }
}