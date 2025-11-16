using System.Threading.Channels;
using Streaming;

namespace Collector.Core.Hubs.SystemAudits;

public sealed class StreamingSystemAuditHub : IStreamingSystemAuditHub
{
    public void SendSystemAudit(SystemAuditContract systemAuditContract)
    {
        SystemAuditChannel.Writer.TryWrite(systemAuditContract);
    }

    public Channel<SystemAuditContract> SystemAuditChannel { get; } = Channel.CreateBounded<SystemAuditContract>(new BoundedChannelOptions(capacity: 100)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });
}