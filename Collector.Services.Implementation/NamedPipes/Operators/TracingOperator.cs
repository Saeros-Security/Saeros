using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes.Operators;

internal static class TracingOperator
{
    public static async Task StreamTraces(TracingRpcService.TracingRpcServiceClient client, Func<TraceContract, CancellationToken, ValueTask> action, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.StreamTraces(new Empty(), cancellationToken: cancellationToken);
            await foreach (var traceContract in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await action(traceContract, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
}