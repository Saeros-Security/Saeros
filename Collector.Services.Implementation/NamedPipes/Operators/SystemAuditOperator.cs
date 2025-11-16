using App.Metrics;
using Collector.Core;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes.Operators;

internal static class SystemAuditOperator
{
    private const string AuditServiceExplanation = "The following machine is reachable: ";
    
    public static async Task StreamSystemAudits(IMetricsRoot metrics, SystemAuditRpcService.SystemAuditRpcServiceClient client, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.StreamSystemAudits(new Empty(), cancellationToken: cancellationToken);
            await foreach (var systemAuditContract in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                if (systemAuditContract.Status is AuditStatus.Success && systemAuditContract.Explanation.StartsWith(AuditServiceExplanation))
                {
                    var today = DateTime.Today.ToString("O");
                    metrics.Measure.Gauge.SetValue(MetricOptions.Computers, new MetricTags(["date", "name"], [today, systemAuditContract.Name]), 1);
                }
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
}