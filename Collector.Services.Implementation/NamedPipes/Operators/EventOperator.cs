using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes.Operators;

internal static class EventOperator
{
    public static async Task StreamEventCount(EventRpcService.EventRpcServiceClient client, Func<EventContract, CancellationToken, ValueTask> action, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.StreamEventCount(new Empty(), cancellationToken: cancellationToken);
            await foreach (var contract in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await action(contract, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
}