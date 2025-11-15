using Collector.Core.Hubs.Processes;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Streaming;

namespace Collector.Services.Implementation.Agent.Processes;

public sealed class ProcessTreeServiceAgent(ILogger<ProcessTreeServiceAgent> logger, IHostApplicationLifetime applicationLifetime, IStreamingProcessHub streamingProcessHub) : ProcessTreeService(logger, applicationLifetime, expiration: TimeSpan.FromSeconds(5))
{
    protected override void SendTree(ProcessKey key, ProcessTree processTree)
    {
        streamingProcessHub.SendProcessTree(new ProcessTreeContract
        {
            WorkstationName = key.WorkstationName,
            Domain = key.Domain,
            LogonId = key.LogonId,
            ProcessId = key.ProcessId,
            ProcessName = key.ProcessName,
            ProcessTree = processTree.Value
        });
    }
}