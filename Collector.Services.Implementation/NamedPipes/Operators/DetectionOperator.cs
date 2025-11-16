using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes.Operators;

internal static class DetectionOperator
{
    public static async Task StreamDetectionsAsync(DetectionRpcService.DetectionRpcServiceClient client, Func<DetectionContract, CancellationToken, ValueTask> action, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.StreamDetections(new Empty(), cancellationToken: cancellationToken);
            await foreach (var detectionContract in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await action(detectionContract, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
}