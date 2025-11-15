using System.Text.Json.Serialization;
using Collector.Core.Extensions;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Shared;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;

public class Process4689(string domain, string workstationName, DateTime date, string processId, string processName) : ProcessTracer(domain, workstationName, date)
{
    public static Tracer ToProcess(WinEvent winEvent, Action<uint, string, DateTime> onTermination)
    {
        if (winEvent.EventData.TryGetValue(nameof(ProcessName), out var pName) &&
            winEvent.EventData.TryGetValue(nameof(ProcessId), out var pid))
        {
            onTermination(pid.ParseUnsigned(), pName, winEvent.SystemTime.ToUniversalTime());
        }

        return DefaultTracer.Instance; // We don't need 4689 on Bridge side
    }

    [JsonPropertyName("ProcessId")]
    public string ProcessId { get; } = processId;

    [JsonPropertyName("ProcessName")]
    public string ProcessName { get; } = processName;
}