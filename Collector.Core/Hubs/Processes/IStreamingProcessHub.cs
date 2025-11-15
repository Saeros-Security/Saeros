using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.Processes;

public interface IStreamingProcessHub : IProcessTreeForwarder
{
    void SendProcessTree(ProcessTreeContract processTreeContract);
}