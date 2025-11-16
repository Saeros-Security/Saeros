using Shared;
using Streaming;
using System.Text.Json.Serialization;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Extensions;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;

public class LogonTracer(string domain, string workstationName, DateTime date) : Tracer
{
    private static readonly HashSet<ushort> EventIds = [4624, 4625];

    protected LogonTracer(string domain, DateTime date) : this(domain, workstationName: string.Empty, date)
    {
        
    }
    
    [JsonPropertyName("Domain")]
    public string Domain { get; } = domain;
    
    [JsonPropertyName("WorkstationName")]
    public string WorkstationName { get; } = workstationName;

    [JsonPropertyName("Date")]
    public DateTime Date { get; } = date;

    private static bool Compatible(WinEvent winEvent) => EventIds.Contains(winEvent.EventId);

    public static async ValueTask<Tracer> CreateAsync(WinEvent winEvent, ILogonStore logonStore, CancellationToken cancellationToken)
    {
        if (!Compatible(winEvent)) return DefaultTracer.Instance;
        return winEvent.EventId switch
        {
            4624 => await Logon4624.ToLogonAsync(winEvent, logonStore, cancellationToken),
            4625 => await Logon4625.ToLogonAsync(winEvent, logonStore, cancellationToken),
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