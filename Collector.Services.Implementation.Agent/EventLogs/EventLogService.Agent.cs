using System.Diagnostics.Eventing.Reader;
using Collector.ActiveDirectory.Helpers.PolicyRegistry;
using Collector.Core.Extensions;
using Collector.Core.Services;
using Collector.Services.Abstractions.Detections;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Abstractions.Processes;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Abstractions.Tracing;
using Collector.Services.Implementation.Agent.EventLogs.Consumers;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.Mof;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.Polling;
using Collector.Services.Implementation.Agent.EventLogs.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Helpers;
using Shared.Streaming.Hubs;

namespace Collector.Services.Implementation.Agent.EventLogs;

public sealed class EventLogServiceAgent(ILogger<EventLogServiceAgent> logger, IEventProviderServiceReader eventProviderServiceReader, IRuleService ruleService, IDetectionService eventLogIngestor, IProcessTreeService processTreeService, ITracingService tracingService, IGeolocationService geolocationService, IProcessLifecycleObserver processLifecycleObserver, IPeService peService, IHostApplicationLifetime hostApplicationLifetime)
    : EventLogServiceBase(logger, ruleService, eventLogIngestor, processTreeService, tracingService, hostApplicationLifetime)
{
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await ConsumeAuditPolicyPreferenceAsync(cancellationToken);
    }

    private async Task ConsumeAuditPolicyPreferenceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var ruleAuditPolicyPreference in RuleService.AuditPolicyPreferenceChannel.ReadAllAsync(cancellationToken))
            {
                StartNewConsumptionSubject.OnNext(new ConsumptionParameters(ruleAuditPolicyPreference.Preference, ProviderChanged: false));
            }
        }
        catch (OperationCanceledException)
        {
            
        }
    }
    
    protected override Task<ICollection<EventLogConsumer>> CreateConsumersAsync(ConsumptionParameters parameters, CancellationToken cancellationToken)
    {
        var etwEventLogConsumer = new ETWEventLogConsumer(logger, RuleService, eventProviderServiceReader, geolocationService, processLifecycleObserver, peService, onEvent: OnEvent, cancellationToken);
        var mofEventLogConsumer = new MofEventLogConsumer(logger, RuleService, eventProviderServiceReader, onEvent: OnEvent, cancellationToken);
        var pollingEventLogConsumer = new PollingEventLogConsumer(logger, RuleService, eventProviderServiceReader, onEvent: OnEvent, cancellationToken);

        if (DomainHelper.DomainJoined)
        {
            if (parameters.AuditPolicyPreference == RuleHub.AuditPolicyPreference.Override)
            {
                logger.LogInformation("Configuring channels...");
                var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                channels.AddRange(etwEventLogConsumer.EnumerateChannels());
                channels.AddRange(mofEventLogConsumer.EnumerateChannels());
                channels.AddRange(pollingEventLogConsumer.EnumerateChannels());
        
                PolicyRegistryHelper.SetGroupPolicyRegistry(GroupPolicyObjectHelper.LocalGpoPath, channels);
                logger.LogInformation("Configured {Count} channels", channels.Count);
            }
            else
            {
                PolicyRegistryHelper.SetGroupPolicyRegistry(GroupPolicyObjectHelper.LocalGpoPath, channels: new HashSet<string>());
            }
        }
        else
        {
            if (parameters.AuditPolicyPreference == RuleHub.AuditPolicyPreference.Override)
            {
                logger.LogInformation("Configuring channels...");
                var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                channels.AddRange(etwEventLogConsumer.EnumerateChannels());
                channels.AddRange(mofEventLogConsumer.EnumerateChannels());
                channels.AddRange(pollingEventLogConsumer.EnumerateChannels());

                var misconfiguredChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var channelName in channels)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (!ChannelHelper.EnableChannel(logger, channelName))
                        {
                            misconfiguredChannels.Add(channelName);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogWarning("Cancellation has occurred");
                        break;
                    }
                    catch (EventLogException)
                    {
                        continue;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error has occurred");
                    }
                }

                channels.ExceptWith(misconfiguredChannels);
                logger.LogInformation("Configured {Count} channels", channels.Count);
            }
        }

        return Task.FromResult<ICollection<EventLogConsumer>>([etwEventLogConsumer, mofEventLogConsumer, pollingEventLogConsumer]);
    }
}