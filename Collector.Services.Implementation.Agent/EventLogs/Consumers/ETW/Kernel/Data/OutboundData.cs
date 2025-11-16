using Collector.Core.Extensions;
using Collector.Services.Abstractions.Tracing;
using ConcurrentCollections;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Kernel.Data;

public sealed class OutboundData : KernelData
{
    public OutboundData(string computer, string processName) : base(computer)
    {
        ProcessName = processName;
        Countries = [];
    }
    
    private OutboundData(string computer, string processName, long outbound, ConcurrentHashSet<string> countries) : this(computer, processName)
    {
        _outbound = outbound;
        Countries.AddRange(countries);
    }

    public ConcurrentHashSet<string> Countries { get; }
    public string ProcessName { get; }
    
    private long _outbound;
    public long Outbound => _outbound;

    public void IncreaseSize(uint value)
    {
        Interlocked.Add(ref _outbound, value);
    }
    
    public OutboundData Swap()
    {
        return new OutboundData(Computer, ProcessName, outbound: Interlocked.Exchange(ref _outbound, 0L), Countries);
    }

    public void AddCountry(string country)
    {
        Countries.Add(country);
    }
}