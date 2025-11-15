using Collector.Databases.Implementation.Caching.LRU;
using Microsoft.Extensions.Logging;
using Microsoft.O365.Security.ETW;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Kernel;

internal sealed class ProcessKernelConsumer(ILogger logger) : AbstractKernelConsumer(logger)
{
    private const string ProcessId = nameof(ProcessId);
    private const string ParentId = nameof(ParentId);
    private const string CommandLine =  nameof(CommandLine);
    
    public override void OnNext(IEventRecord eventRecord)
    {
        if (eventRecord.Opcode is not 1) return;
        if (eventRecord.TryGetUInt32(ProcessId, out var processId))
        {
            var pId = processId.ToString();
            if (eventRecord.TryGetUnicodeString(CommandLine, out var commandLine))
            {
                Lrus.CommandLineByProcessId.AddOrUpdate(pId, commandLine);
            }

            if (eventRecord.TryGetUInt32(ParentId, out var parentId))
            {
                Lrus.ParentProcessIdByProcessId.AddOrUpdate(pId, parentId.ToString());
            }
        }
    }

    public override void Dispose()
    {
    }
}