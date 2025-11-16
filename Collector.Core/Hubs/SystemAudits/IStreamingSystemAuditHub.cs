using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.SystemAudits;

public interface IStreamingSystemAuditHub : ISystemAuditForwarder
{
    void SendSystemAudit(SystemAuditContract systemAuditContract);
}