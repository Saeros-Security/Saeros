using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes.Operators;

internal static class MetricOperator
{
    public static async Task StreamMetrics(MetricRpcService.MetricRpcServiceClient client, Func<MetricContract, CancellationToken, ValueTask> action, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.StreamMetrics(new Empty(), cancellationToken: cancellationToken);
            await foreach (var metricContract in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await action(metricContract, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
}