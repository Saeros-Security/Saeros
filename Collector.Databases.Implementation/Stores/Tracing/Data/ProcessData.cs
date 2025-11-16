using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;

namespace Collector.Databases.Implementation.Stores.Tracing.Data;

internal sealed class ProcessData(string domain, string workstationName, string user, string userSid, string processId, string processName, string processTree, string commandLine, string parentProcessName, bool elevated) : TracingData(nameof(Process))
{
    public string Domain { get; } = domain;
    public string WorkstationName { get; } = workstationName;
    public string User { get; } = user;
    public string UserSid { get; } = userSid;
    public string ProcessId { get; } = processId;
    public string ProcessName { get; } = processName;
    public string ProcessTree { get; } = processTree;
    public string CommandLine { get; } = commandLine;
    public string ParentProcessName { get; } = parentProcessName;
    public bool Elevated { get; } = elevated;
}