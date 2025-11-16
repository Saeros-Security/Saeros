using System.Security.Cryptography.X509Certificates;
using App.Metrics;
using Collector.Core.SystemAudits;
using Collector.Services.Abstractions.NamedPipes;
using Collector.Services.Implementation.NamedPipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Services.Implementation.Agent.NamedPipes;

internal sealed class NamedPipeAgent
{
    private readonly INamedPipe _namedPipe;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IMetricsRoot _metrics;
    private readonly IDetectionForwarder _detectionForwarder;
    private readonly IRuleForwarder _ruleForwarder;
    private readonly IProcessTreeForwarder _processTreeForwarder;
    private readonly ITracingForwarder _tracingForwarder;
    private readonly IEventForwarder _eventForwarder;
    private readonly IMetricForwarder _metricForwarder;
    private readonly Action<SystemAuditKey, string, AuditStatus> _onAudit;

    public NamedPipeAgent(ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        IMetricsRoot metrics,
        IDetectionForwarder detectionForwarder,
        IRuleForwarder ruleForwarder,
        IProcessTreeForwarder processTreeForwarder,
        ITracingForwarder tracingForwarder,
        IEventForwarder eventForwarder,
        IMetricForwarder metricForwarder,
        Action<SystemAuditKey, string, AuditStatus> onAudit)
    {
        _loggerFactory = loggerFactory;
        _applicationLifetime = applicationLifetime;
        _metrics = metrics;
        _detectionForwarder = detectionForwarder;
        _ruleForwarder = ruleForwarder;
        _processTreeForwarder = processTreeForwarder;
        _tracingForwarder = tracingForwarder;
        _eventForwarder = eventForwarder;
        _metricForwarder = metricForwarder;
        _onAudit = onAudit;
        _namedPipe = CreateNamedPipe();
    }

    private INamedPipe CreateNamedPipe()
    {
        return new NamedPipe(_loggerFactory,
            _applicationLifetime,
            _metrics,
            detectionAction: (contract, ct) => _detectionForwarder.DetectionChannel.Writer.WriteAsync(contract, ct),
            processAction: (contract, ct) => _processTreeForwarder.ProcessTreeChannel.Writer.WriteAsync(contract, ct),
            traceAction: (contract, ct) => _tracingForwarder.TraceChannel.Writer.WriteAsync(contract, ct),
            eventAction: (contract, ct) => _eventForwarder.EventChannel.Writer.WriteAsync(contract, ct),
            ruleAction: (contract, ct) => _ruleForwarder.RuleChannel.Writer.WriteAsync(contract, ct),
            ruleUpdateAction: (contract, ct) => _ruleForwarder.RuleUpdateChannel.Writer.WriteAsync(contract, ct),
            metricAction: (contract, ct) => _metricForwarder.MetricChannel.Writer.WriteAsync(contract, ct),
            onAudit: _onAudit);
    }

    public Channels Channels => _namedPipe.Channels;

    public async Task StartAsync(string domainController, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        await _namedPipe.StreamAsync(domainController, certificate, onCallException: _ => {}, cancellationToken);
    }
}