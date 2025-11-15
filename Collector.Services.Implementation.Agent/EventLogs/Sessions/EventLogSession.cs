using Collector.Services.Implementation.Agent.EventLogs.Pipelines;
using Microsoft.O365.Security.ETW;

namespace Collector.Services.Implementation.Agent.EventLogs.Sessions;

public sealed class EventLogSession(string name, string? channelName, Lazy<ITrace> trace, WinEventLogPipeline pipeline, SessionType sessionType)
{
    public string Name { get; } = name;
    public string? ChannelName { get; } = channelName;
    public Lazy<ITrace> Trace { get; } = trace;
    public WinEventLogPipeline Pipeline { get; } = pipeline;
    public SessionType SessionType { get; } = sessionType;

    public bool TryQueryStats(out TraceStats stats)
    {
        stats = new TraceStats();
        if (Trace.IsValueCreated)
        {
            stats = Trace.Value.QueryStats();
            return true;
        }

        return false;
    }
}