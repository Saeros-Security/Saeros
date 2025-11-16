namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;

public abstract class ProcessBase(uint processId, string processName, DateTimeOffset time)
{
    public uint ProcessId { get; } = processId;
    public string ProcessName { get; } = processName;
    public DateTimeOffset Time { get; } = time;
}

public sealed class ProcessCreation(uint processId, string processName, DateTimeOffset time) : ProcessBase(processId, processName, time);
public sealed class ProcessTermination(uint processId, string processName, DateTimeOffset time) : ProcessBase(processId, processName, time);