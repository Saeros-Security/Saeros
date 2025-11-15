using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.Detections;

public interface IStreamingDetectionHub : IDetectionForwarder
{
    void SendDetection(DetectionContract detectionContract);
}