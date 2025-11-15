using System.Reactive.Linq;
using Collector.Core.Hubs.Events;
using Collector.Core.Hubs.Tracing;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;
using Microsoft.Extensions.Logging;
using Streaming;

namespace Collector.Services.Implementation.Agent.Tracing;

public sealed class TracingAgent : TracingService
{
    private readonly IStreamingTraceHub streamingTraceHub;
    private readonly IStreamingEventHub streamingEventHub;
    private readonly IDisposable _subscription;

    public TracingAgent(ILogger<TracingAgent> logger, IStreamingTraceHub streamingTraceHub, IStreamingEventHub streamingEventHub, ILogonStore logonStore, IProcessLifecycleObserver processLifecycleObserver) : base(logger, logonStore, processLifecycleObserver)
    {
        this.streamingTraceHub = streamingTraceHub;
        this.streamingEventHub = streamingEventHub;
        _subscription = Observable.Interval(TimeSpan.FromSeconds(5)).Do(_ =>
        {
            foreach (var logon in logonStore.EnumerateSuccessLogons())
            {
                Trace(new SuccessLogonTracer(logon.Count, logon.TargetAccount, logon.TargetComputer, logon.LogonType, logon.SourceComputer, logon.SourceIpAddress).ToContract());
            }
            
            foreach (var logon in logonStore.EnumerateFailureLogons())
            {
                Trace(new FailureLogonTracer(logon.Count, logon.TargetAccount, logon.TargetComputer, logon.LogonType, logon.SourceComputer, logon.SourceIpAddress).ToContract());
            }
        }).Subscribe();
    }

    protected override void Trace(TraceContract contract)
    {
        streamingTraceHub.Send(contract);
    }

    protected override void Trace(EventContract contract)
    {
        streamingEventHub.Send(contract);
    }

    public override void Dispose()
    {
        _subscription.Dispose();
    }
}