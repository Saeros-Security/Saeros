using System.Text;
using System.Threading.Channels;
using App.Metrics;
using Collector.ActiveDirectory.Helpers;
using Collector.Core.Hubs.Rules;
using Collector.Core.Services;
using Collector.Core.SystemAudits;
using Collector.Services.Abstractions.DomainControllers;
using Collector.Services.Abstractions.NamedPipes;
using Collector.Services.Implementation.Agent.Helpers;
using Collector.Services.Implementation.Agent.NamedPipes;
using Collector.Services.Implementation.Agent.NamedPipes.Broadcasters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Helpers;
using Shared.Models.Console.Requests;
using Shared.Streaming.Hubs;
using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Services.Implementation.Agent.DomainControllers;

public sealed class DomainControllerService(ILogger<DomainControllerService> logger,
    IHostApplicationLifetime applicationLifetime,
    ILoggerFactory loggerFactory,
    IMetricsRoot metrics,
    AgentCertificateHelper agentCertificateHelper,
    IDetectionForwarder detectionForwarder,
    IRuleForwarder ruleForwarder,
    IProcessTreeForwarder processTreeForwarder,
    ITracingForwarder tracingForwarder,
    IEventForwarder eventForwarder,
    IMetricForwarder metricForwarder,
    ISystemAuditForwarder systemAuditForwarder,
    IStreamingRuleHub ruleHub,
    ISystemAuditService systemAuditService) : IDomainControllerService
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var certificate = await agentCertificateHelper.GetServerCertificateAsync();
        if (certificate == null)
        {
            logger.LogError("Could not retrieve server certificate");
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var domainControllers = await GetDomainControllersAsync(DomainHelper.DomainName, cts.Token);
            var tasks = domainControllers.Select(domainController =>
            {
                var namedPipe = new NamedPipeAgent(loggerFactory,
                    applicationLifetime,
                    metrics,
                    detectionForwarder,
                    ruleForwarder,
                    processTreeForwarder,
                    tracingForwarder,
                    eventForwarder,
                    metricForwarder,
                    onAudit: (key, _, status) =>
                    {
                        if (systemAuditService.TryGetNameExplanation(key, status, out var name, out var explanation))
                        {
                            systemAuditForwarder.SystemAuditChannel.Writer.TryWrite(new SystemAuditContract
                            {
                                Date = DateTimeOffset.UtcNow.Ticks,
                                Status = status,
                                Name = name,
                                Explanation = explanation
                            });
                        }
                    });

                return Task.WhenAll(namedPipe.StartAsync(domainController, certificate, cts.Token),
                    ForwardAsync(ruleHub.SubscribeCreation(applicationLifetime), namedPipe.Channels.RuleCreationChannel, onDelete: channel => channel.Remove(), creation => new CreateRuleResponse(new CreateRule(creation.Request.Content.ToByteArray(), creation.Request.Enabled, creation.Request.WaitIndefinitely, creation.Request.GroupName), (RuleHub.AuditPolicyPreference)creation.Request.AuditPolicyPreference, creation.Response), cts.Token),
                    ForwardAsync(ruleHub.SubscribeDeletion(applicationLifetime), namedPipe.Channels.RuleDeletionChannel, onDelete: channel => channel.RemoveDeletion(), deletion => deletion, cts.Token),
                    ForwardAsync(ruleHub.SubscribeEnablement(applicationLifetime), namedPipe.Channels.RuleEnablementChannel, onDelete: channel => channel.RemoveEnablement(), enablement => enablement, cts.Token),
                    ForwardAsync(ruleHub.SubscribeDisablement(applicationLifetime), namedPipe.Channels.RuleDisablementChannel, onDelete: channel => channel.RemoveDisablement(), disablement => disablement, cts.Token),
                    ForwardAsync(ruleHub.SubscribeCodeUpdate(applicationLifetime), namedPipe.Channels.RuleCodeUpdateChannel, onDelete: channel => channel.Remove(), codeUpdate => new UpdateRuleCodeResponse(new UpdateRuleCode(codeUpdate.CodeUpdate.Id, Encoding.UTF8.GetBytes(codeUpdate.CodeUpdate.Code)), (RuleHub.AuditPolicyPreference)codeUpdate.CodeUpdate.AuditPolicyPreference, codeUpdate.Response), cts.Token));
            }).ToList();

            tasks.Add(DiscoverDomainControllersAsync(DomainHelper.DomainName, domainControllers, cts.Token));
            await Task.WhenAny(tasks);
            await cts.CancelAsync();
        }
    }

    private static async Task ForwardAsync<T, TU>(Channel<T> channel, Channel<TU> destination, Action<Channel<T>> onDelete, Func<T, TU> transform, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
            {
                destination.Writer.TryWrite(transform(message));
            }
        }
        catch (OperationCanceledException)
        {
            onDelete(channel);
        }
    }

    private Task<HashSet<string>> GetDomainControllersAsync(string domain, CancellationToken cancellationToken)
    {
        var policy = Policy<HashSet<string>>.HandleResult(r => r.Count == 0).WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(5)); // Retry when NTDS is not yet available
        return policy.ExecuteAsync(ct =>
        {
            ct.ThrowIfCancellationRequested();
            // Ensure to be the PDC
            var machineName = MachineNameHelper.FullyQualifiedName;
            var primaryDomainController = ActiveDirectoryHelper.GetPrimaryDomainControllerDnsName(logger, domain, ct);
            if (!machineName.Equals(primaryDomainController, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(new HashSet<string>());
            
            var domainControllers = ActiveDirectoryHelper.EnumerateDomainControllers(logger, domain, ct).Select(c => c.serverName).Where(server => !server.Equals(primaryDomainController, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (domainControllers.Count == 0)
            {
                if (systemAuditService.TryGetContract(new KeyValuePair<SystemAuditKey, AuditStatus>(new SystemAuditKey(SystemAuditType.DomainController), AuditStatus.Failure), out var contract))
                {
                    systemAuditForwarder.SystemAuditChannel.Writer.TryWrite(contract);
                }
            }
            else
            {
                if (systemAuditService.TryGetContract(new KeyValuePair<SystemAuditKey, AuditStatus>(new SystemAuditKey(SystemAuditType.DomainController), AuditStatus.Success), out var contract))
                {
                    systemAuditForwarder.SystemAuditChannel.Writer.TryWrite(contract);
                }
            }

            return Task.FromResult(domainControllers);
        }, cancellationToken);
    }

    private async Task DiscoverDomainControllersAsync(string domain, ISet<string> domainControllers, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    var currentDomainControllers = await GetDomainControllersAsync(domain, cancellationToken);
                    currentDomainControllers.ExceptWith(domainControllers);
                    if (currentDomainControllers.Count > 0)
                    {
                        logger.LogInformation("A new domain controller has been discovered");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error has occurred");
                }
            }
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
}