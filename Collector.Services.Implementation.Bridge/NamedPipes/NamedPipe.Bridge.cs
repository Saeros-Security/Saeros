using System.Collections.Concurrent;
using App.Metrics;
using Collector.Core.Services;
using Collector.Core.SystemAudits;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Databases.Abstractions.Stores.Tracing;
using Collector.Services.Abstractions.NamedPipes;
using Collector.Services.Implementation.NamedPipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Helpers;
using Shared.Models.Console.Requests;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Implementation.Bridge.NamedPipes;

public sealed class NamedPipeBridge(ILogger<NamedPipeBridge> logger,
    IHostApplicationLifetime applicationLifetime,
    ILoggerFactory loggerFactory,
    IDetectionRepository detectionRepository,
    IRuleRepository ruleRepository,
    ISettingsRepository settingsRepository,
    ISystemAuditService systemAuditService,
    ITracingStore tracingStore,
    ISettingsStore settingsStore,
    IMetricsRoot metrics)
    : INamedPipeBridge
{
    private readonly ConcurrentDictionary<string, INamedPipe> _namedPipes = new(StringComparer.OrdinalIgnoreCase);

    private INamedPipe CreateNamedPipe(string domain)
    {
        return new NamedPipe(loggerFactory,
            applicationLifetime,
            metrics,
            detectionAction: detectionRepository.InsertAsync,
            processAction: tracingStore.ProcessAsync,
            traceAction: tracingStore.ProcessAsync,
            eventAction: tracingStore.ProcessAsync,
            ruleAction: ruleRepository.InsertAsync,
            ruleUpdateAction: (ruleUpdate, ct) => ruleRepository.UpdateAsync(ruleId: ruleUpdate.Id, date: new DateTimeOffset(ruleUpdate.Updated, offset: TimeSpan.Zero), ct),
            metricAction: settingsRepository.StoreAsync,
            onAudit: (key, serverName, status) =>
            {
                systemAuditService.Add(key, status);
                if (status == AuditStatus.Success)
                {
                    systemAuditService.ServerConnected(domain, serverName);
                }
            });
    }
    
    public async Task ExecuteAsync(string domain, string server, CancellationToken cancellationToken)
    {
        try
        {
            var certificate = CertificateHelper.GetCollectorCertificate();
            var namedPipe = _namedPipes.GetOrAdd(server, valueFactory: _ => CreateNamedPipe(domain));
            await namedPipe.StreamAsync(server, certificate, onCallException: _ =>
            {
                var key = DomainHelper.DomainJoined ? new SystemAuditKey(SystemAuditType.DomainController, server) : new SystemAuditKey(SystemAuditType.Collector);
                systemAuditService.Add(key, AuditStatus.Failure);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
    }

    public async ValueTask SendRuleCreationAsync(CreateRule createRule, TaskCompletionSource<RuleCreationResponseContract> response, CancellationToken cancellationToken, params string[] serverNames)
    {
        if (serverNames.Length == 0)
        {
            foreach (var namedPipe in _namedPipes.Values)
            {
                await namedPipe.Channels.RuleCreationChannel.Writer.WriteAsync(new CreateRuleResponse(createRule, settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride, response), cancellationToken);
            }
        }
        else
        {
            foreach (var serverName in serverNames)
            {
                if (_namedPipes.TryGetValue(serverName, out var namedPipe))
                {
                    await namedPipe.Channels.RuleCreationChannel.Writer.WriteAsync(new CreateRuleResponse(createRule, settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride, response), cancellationToken);
                }
            }
        }
    }

    public async ValueTask SendRuleEnablementAsync(EnableRule enableRule, CancellationToken cancellationToken, params string[] serverNames)
    {
        if (serverNames.Length == 0)
        {
            foreach (var namedPipe in _namedPipes.Values)
            {
                await namedPipe.Channels.RuleEnablementChannel.Writer.WriteAsync(new RuleIdContract { RuleId = enableRule.RuleId, AuditPolicyPreference = (int)(settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride) }, cancellationToken);
            }
        }
        else
        {
            foreach (var serverName in serverNames)
            {
                if (_namedPipes.TryGetValue(serverName, out var namedPipe))
                {
                    await namedPipe.Channels.RuleEnablementChannel.Writer.WriteAsync(new RuleIdContract { RuleId = enableRule.RuleId, AuditPolicyPreference = (int)(settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride) }, cancellationToken);
                }
            }
        }
    }

    public async ValueTask SendRuleDisablementAsync(DisableRule disableRule, CancellationToken cancellationToken, params string[] serverNames)
    {
        if (serverNames.Length == 0)
        {
            foreach (var namedPipe in _namedPipes.Values)
            {
                await namedPipe.Channels.RuleDisablementChannel.Writer.WriteAsync(new RuleIdContract { RuleId = disableRule.RuleId, AuditPolicyPreference = (int)(settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride) }, cancellationToken);
            }
        }
        else
        {
            foreach (var serverName in serverNames)
            {
                if (_namedPipes.TryGetValue(serverName, out var namedPipe))
                {
                    await namedPipe.Channels.RuleDisablementChannel.Writer.WriteAsync(new RuleIdContract { RuleId = disableRule.RuleId, AuditPolicyPreference = (int)(settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride) }, cancellationToken);
                }
            }
        }
    }

    public async ValueTask SendRuleDeletionAsync(DeleteRule deleteRule, CancellationToken cancellationToken)
    {
        foreach (var namedPipe in _namedPipes.Values)
        {
            await namedPipe.Channels.RuleDeletionChannel.Writer.WriteAsync(new RuleIdContract { RuleId = deleteRule.RuleId, AuditPolicyPreference = (int)(settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride) }, cancellationToken);
        }
    }

    public async ValueTask SendRuleCodeUpdateAsync(UpdateRuleCode updateRuleCode, TaskCompletionSource<RuleCodeUpdateResponseContract> response, CancellationToken cancellationToken)
    {
        foreach (var namedPipe in _namedPipes.Values)
        {
            await namedPipe.Channels.RuleCodeUpdateChannel.Writer.WriteAsync(new UpdateRuleCodeResponse(updateRuleCode, settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride, response), cancellationToken);
        }
    }

    public async ValueTask SendAuditPolicyPreference(RuleHub.AuditPolicyPreference preference, string[] servers, CancellationToken cancellationToken)
    {
        foreach (var serverName in servers)
        {
            if (_namedPipes.TryGetValue(serverName, out var namedPipe))
            {
                await namedPipe.Channels.AuditPolicyPreferenceChannel.Writer.WriteAsync(preference, cancellationToken);
            }
        }
    }
}