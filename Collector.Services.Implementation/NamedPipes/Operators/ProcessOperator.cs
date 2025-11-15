using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes.Operators;

internal static class ProcessOperator
{
    public static async Task StreamProcessTrees(ProcessRpcService.ProcessRpcServiceClient client, Func<ProcessTreeContract, CancellationToken, ValueTask> action, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.StreamProcessTrees(new Empty(), cancellationToken: cancellationToken);
            await foreach (var processTreeContract in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await action(processTreeContract, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
}