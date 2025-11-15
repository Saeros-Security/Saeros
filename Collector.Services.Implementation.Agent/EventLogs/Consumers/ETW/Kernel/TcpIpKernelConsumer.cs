using System.Collections.Concurrent;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Collector.Core.Services;
using Collector.Databases.Implementation.Extensions;
using Collector.Services.Abstractions.EventLogs.Pipelines;
using Collector.Services.Abstractions.Tracing;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Kernel.Data;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;
using Microsoft.Extensions.Logging;
using Microsoft.O365.Security.ETW;
using Shared.Helpers;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Kernel;

internal sealed class TcpIpKernelConsumer : AbstractKernelConsumer, IObserver<ProcessBase>
{
    private const string ProcessId = "PID";
    private const string Size = "size";
    private const string SourceAddress = "saddr";
    private const string DestinationAddress = "daddr";
    private const string TcpIp = nameof(TcpIp);
    private readonly IDisposable _subscription;
    private readonly IGeolocationService _geolocationService;
    private readonly IEventLogPipeline<KernelData> _eventLogPipeline;
    private readonly ConcurrentDictionary<uint, OutboundData> _outboundByProcessId = new();
    
    public TcpIpKernelConsumer(ILogger logger, IGeolocationService geolocationService, IObservable<ProcessBase> processObservable, IEventLogPipeline<KernelData> eventLogPipeline) : base(logger)
    {
        _geolocationService = geolocationService;
        _eventLogPipeline = eventLogPipeline;
        _subscription = new CompositeDisposable(Observable.Interval(TimeSpan.FromSeconds(5)).Do(_ =>
        {
            foreach (var kvp in _outboundByProcessId)
            {
                var data = kvp.Value.Swap();
                if (data.Outbound > 0L)
                {
                    Push(data);
                }
            }
        }).Subscribe(), processObservable.Subscribe(observer: this));
    }
    
    public override void OnNext(IEventRecord eventRecord)
    {
        if (eventRecord.TaskName.Equals(TcpIp, StringComparison.Ordinal) && eventRecord.Opcode is 10 or 26) // SendIPV4 || SendIPV6
        {
            if (!eventRecord.TryGetUInt32(ProcessId, out var pid)) return;
            if (!_outboundByProcessId.ContainsKey(pid)) return;
            if (!eventRecord.TryGetIPAddress(SourceAddress, out var sourceAddress))
            {
                return;
            }

            if (!eventRecord.TryGetIPAddress(DestinationAddress, out var destinationAddress))
            {
                return;
            }

            if (sourceAddress.IsPrivate() && destinationAddress.IsPrivate()) return;
            if (eventRecord.TryGetUInt32(Size, out var size))
            {
                AddSizeAndCountry(pid, size, sourceAddress, destinationAddress);
            }
        }
    }
    
    public void OnNext(ProcessBase value)
    {
        if (value is ProcessCreation)
        {
            _outboundByProcessId.TryAdd(value.ProcessId, new OutboundData(MachineNameHelper.FullyQualifiedName, value.ProcessName));
        }
        else if (value is ProcessTermination && _outboundByProcessId.TryRemove(value.ProcessId, out var outbound))
        {
            var data = outbound.Swap();
            if (data.Outbound > 0L)
            {
                Push(data);
            }
        }
    }

    private void Push(OutboundData data)
    {
        _eventLogPipeline.Push(data);
    }

    private void AddSizeAndCountry(uint processId, uint size, IPAddress source, IPAddress destination)
    {
        if (_outboundByProcessId.TryGetValue(processId, out var outbound))
        {
            outbound.IncreaseSize(size);
            
            var srcIp = source.ToString();
            var dstIp = destination.ToString();
            if (_geolocationService.TryResolve(srcIp, out var code, out _))
            {
                outbound.AddCountry(code);
            }

            if (_geolocationService.TryResolve(dstIp, out code, out _))
            {
                outbound.AddCountry(code);
            }
        }
    }

    public override void Dispose()
    {
        _subscription.Dispose();
    }
}