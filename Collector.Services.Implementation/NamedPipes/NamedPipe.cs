using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using App.Metrics;
using Collector.Core.SystemAudits;
using Collector.Services.Abstractions.NamedPipes;
using Collector.Services.Implementation.NamedPipes.Factories;
using Collector.Services.Implementation.NamedPipes.Operators;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes;

public sealed class NamedPipe(
    ILoggerFactory loggerFactory,
    IHostApplicationLifetime applicationLifetime,
    IMetricsRoot metrics,
    Func<DetectionContract, CancellationToken, ValueTask> detectionAction,
    Func<MetricContract, CancellationToken, ValueTask> metricAction,
    Func<ProcessTreeContract, CancellationToken, ValueTask> processAction,
    Func<RuleContract, CancellationToken, ValueTask> ruleAction,
    Func<RuleUpdateContract, CancellationToken, ValueTask> ruleUpdateAction,
    Func<TraceContract, CancellationToken, ValueTask> traceAction,
    Func<EventContract, CancellationToken, ValueTask> eventAction,
    Action<SystemAuditKey, string, AuditStatus> onAudit)
    : INamedPipe
{
    private int _initialized;

    public Channels Channels { get; } = new(Channel.CreateUnbounded<CreateRuleResponse>(), Channel.CreateUnbounded<RuleIdContract>(), Channel.CreateUnbounded<RuleIdContract>(), Channel.CreateUnbounded<RuleIdContract>(), Channel.CreateUnbounded<UpdateRuleCodeResponse>(), Channel.CreateUnbounded<RuleHub.AuditPolicyPreference>());

    public async Task StreamAsync(string serverName, X509Certificate2 certificate, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, comparand: 0) != 0) return;
        while (!cancellationToken.IsCancellationRequested)
        {
            using var channel = CreateChannel(loggerFactory, applicationLifetime, onAudit, serverName, certificate);
            var detectionClient = new DetectionRpcService.DetectionRpcServiceClient(channel);
            var ruleClient = new RuleRpcService.RuleRpcServiceClient(channel);
            var processTreeClient = new ProcessRpcService.ProcessRpcServiceClient(channel);
            var tracingClient = new TracingRpcService.TracingRpcServiceClient(channel);
            var eventClient = new EventRpcService.EventRpcServiceClient(channel);
            var systemAuditClient = new SystemAuditRpcService.SystemAuditRpcServiceClient(channel);
            var metricClient = new MetricRpcService.MetricRpcServiceClient(channel);
            using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await Task.WhenAny(DetectionOperator.StreamDetectionsAsync(detectionClient, action: detectionAction, onCallException, innerCts.Token),
                ProcessOperator.StreamProcessTrees(processTreeClient, action: processAction, onCallException, innerCts.Token),
                TracingOperator.StreamTraces(tracingClient, action: traceAction, onCallException, innerCts.Token),
                EventOperator.StreamEventCount(eventClient, action: eventAction, onCallException, innerCts.Token),
                RuleOperator.StreamRulesAsync(ruleClient, action: ruleAction, onCallException, innerCts.Token),
                RuleOperator.StreamRuleUpdatesAsync(ruleClient, action: ruleUpdateAction, onCallException, innerCts.Token),
                RuleOperator.StreamRuleCreationAsync(ruleClient, Channels.RuleCreationChannel, onCallException, innerCts.Token),
                RuleOperator.StreamRuleEnablementAsync(ruleClient, Channels.RuleEnablementChannel, onCallException, innerCts.Token),
                RuleOperator.StreamRuleDisablementAsync(ruleClient, Channels.RuleDisablementChannel, onCallException, innerCts.Token),
                RuleOperator.StreamRuleDeletionAsync(ruleClient, Channels.RuleDeletionChannel, onCallException, innerCts.Token),
                RuleOperator.StreamRuleCodeUpdateAsync(ruleClient, Channels.RuleCodeUpdateChannel, onCallException, innerCts.Token),
                RuleOperator.StreamAuditPolicyPreferencesAsync(ruleClient, Channels.AuditPolicyPreferenceChannel, onCallException, innerCts.Token),
                SystemAuditOperator.StreamSystemAudits(metrics, systemAuditClient, onCallException, innerCts.Token),
                MetricOperator.StreamMetrics(metricClient, action: metricAction, onCallException, innerCts.Token));
            await innerCts.CancelAsync();
        }
    }

    private static GrpcChannel CreateChannel(ILoggerFactory loggerFactory, IHostApplicationLifetime applicationLifetime, Action<SystemAuditKey, string, AuditStatus> onAudit, string serverName, X509Certificate2 certificate2)
    {
        var connectionFactory = new NamedPipeConnectionFactory(applicationLifetime, serverName, onAudit);
        var socketsHttpHandler = new SocketsHttpHandler
        {
            ConnectCallback = connectionFactory.ConnectAsync
        };

        socketsHttpHandler.SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            ClientCertificates = new X509Certificate2Collection()
        };

        socketsHttpHandler.SslOptions.ClientCertificates.Add(certificate2);
        return GrpcChannel.ForAddress(GetAddress(serverName), new GrpcChannelOptions
        {
            HttpHandler = socketsHttpHandler,
            MaxReceiveMessageSize = null,
            MaxRetryAttempts = null,
            LoggerFactory = loggerFactory,
            DisposeHttpClient = true
        });
    }

    private static string GetAddress(string serverName)
    {
        return $"https://{serverName}";
    }
}