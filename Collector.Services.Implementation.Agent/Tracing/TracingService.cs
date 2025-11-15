using Collector.Core.Extensions;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Kernel;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;
using Collector.Services.Abstractions.Tracing;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Kernel.Data;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;
using Microsoft.Extensions.Logging;
using Shared;
using Streaming;

namespace Collector.Services.Implementation.Agent.Tracing;

public abstract class TracingService(ILogger<TracingService> logger, ILogonStore logonStore, IProcessLifecycleObserver processLifecycleObserver) : ITracingService
{
    public async ValueTask TraceAsync(WinEvent winEvent, CancellationToken cancellationToken)
    {
        try
        {
            var tracer = await GetTracerAsync(winEvent, logonStore, processLifecycleObserver, cancellationToken);
            if (tracer is not DefaultTracer)
            {
                Trace(tracer.ToContract());
            }
        }
        catch (Exception ex)
        {
            logger.Throttle(nameof(TracingService), itself => itself.LogError(ex, "Could not process trace"), expiration: TimeSpan.FromMinutes(1));
        }
    }
    
    public ValueTask TraceAsync(KernelData kernelData)
    {
        try
        {
            if (kernelData is OutboundData networkData)
            {
                Trace(new NetworkTracer(networkData.Computer, networkData.ProcessName, networkData.Outbound, networkData.Countries.ToHashSet()).ToContract());
            }
        }
        catch (Exception ex)
        {
            logger.Throttle(nameof(TracingService), itself => itself.LogError(ex, "Could not process trace"), expiration: TimeSpan.FromMinutes(1));
        }
        
        return ValueTask.CompletedTask;
    }

    public ValueTask TraceAsync(IDictionary<int, long> eventCountById)
    {
        try
        {
            var contract = new EventContract();
            contract.EventCountById.MergeFrom(eventCountById.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            Trace(contract);
        }
        catch (Exception ex)
        {
            logger.Throttle(nameof(TracingService), itself => itself.LogError(ex, "Could not process trace"), expiration: TimeSpan.FromMinutes(1));
        }
        
        return ValueTask.CompletedTask;
    }

    private static async ValueTask<Tracer> GetTracerAsync(WinEvent winEvent, ILogonStore logonStore, IProcessLifecycleObserver processLifecycleObserver, CancellationToken cancellationToken)
    {
        var logonTracer = await LogonTracer.CreateAsync(winEvent, logonStore, cancellationToken);
        if (logonTracer is not DefaultTracer) return logonTracer;
        
        var processTracer = await ProcessTracer.CreateAsync(winEvent, logonStore, onCreation: (pId, pName, time) => processLifecycleObserver.OnNext(new ProcessCreation(pId, pName, time)), onTermination: (pId, pName, time) => processLifecycleObserver.OnNext(new ProcessTermination(pId, pName, time)), cancellationToken);
        if (processTracer is not DefaultTracer) return processTracer;
        
        return DefaultTracer.Instance;
    }
    
    protected abstract void Trace(TraceContract contract);
    protected abstract void Trace(EventContract contract);
    public abstract void Dispose();
}