using Shared;
using Streaming;

namespace Collector.Core.Services;

public interface IIntegrationService : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
    void Export(DetectionContract detectionContract, WinEvent winEvent, string tactic, string technique, string subTechnique);
}