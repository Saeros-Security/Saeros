using System.Threading.Channels;
using Collector.Core.Extensions;
using Collector.Services.Abstractions.EventLogs.Pipelines;
using Collector.Services.Abstractions.Tracing;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Kernel;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Agent.EventLogs.Pipelines;

internal sealed class KernelDataPipeline : IEventLogPipeline<KernelData>
{
    private readonly Channel<KernelData> _kernelDataChannel;

    public KernelDataPipeline(ILogger logger)
    {
        var options = new BoundedChannelOptions(capacity: 1024 * Environment.ProcessorCount)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };

        _kernelDataChannel = Channel.CreateBounded<KernelData>(options, _ => logger.Throttle(nameof(KernelData), itself => itself.LogWarning("Kernel data was dropped"), expiration: TimeSpan.FromMinutes(1)));
    }
    public bool Push(KernelData kernelData)
    {
        return _kernelDataChannel.Writer.TryWrite(kernelData);
    }

    public IAsyncEnumerable<KernelData> Consume(CancellationToken cancellationToken)
    {
        return _kernelDataChannel.Reader.ReadAllAsync(cancellationToken);
    }
}