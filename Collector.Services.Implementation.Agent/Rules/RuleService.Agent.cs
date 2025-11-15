using System.Reactive.Subjects;
using System.Threading.Channels;
using Collector.ActiveDirectory.AuditPolicies;
using Collector.ActiveDirectory.Helpers;
using Collector.ActiveDirectory.Helpers.AuditPolicies;
using Collector.Core;
using Collector.Core.Helpers;
using Collector.Core.Hubs.Rules;
using Collector.Databases.Abstractions.Repositories.AuditPolicies;
using Collector.Databases.Abstractions.Repositories.RuleConfigurations;
using Collector.Detection.Rules;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Implementation.Agent.EventLogs.Helpers;
using Collector.Services.Implementation.Agent.NamedPipes.Broadcasters;
using Collector.Services.Implementation.Agent.Rules.Extensions;
using Collector.Services.Implementation.Rules;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Helpers;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Streaming.Hubs;
using Streaming;
using Vanara.PInvoke;

namespace Collector.Services.Implementation.Agent.Rules;

public sealed class RuleServiceAgent : RuleService
{
    private readonly Subject<ConsumptionParameters> _eventLogProviderSubject = new();
    private readonly IStreamingRuleHub _streamingRuleHub;
    private readonly IAuditPoliciesRepository _auditPoliciesRepository;

    public RuleServiceAgent(ILogger<RuleService> logger, IHostApplicationLifetime applicationLifetime, IEventProviderServiceReader eventProviderService, IRuleConfigurationsRepository ruleConfigurationsRepository, IStreamingRuleHub streamingRuleHub, IAuditPoliciesRepository auditPoliciesRepository) : base(logger, eventProviderService, ruleConfigurationsRepository)
    {
        _streamingRuleHub = streamingRuleHub;
        _auditPoliciesRepository = auditPoliciesRepository;
        RuleCreationChannel = _streamingRuleHub.SubscribeCreation(applicationLifetime);
        RuleEnablementChannel = _streamingRuleHub.SubscribeEnablement(applicationLifetime);
        RuleDisablementChannel = _streamingRuleHub.SubscribeDisablement(applicationLifetime);
        RuleDeletionChannel = _streamingRuleHub.SubscribeDeletion(applicationLifetime);
        RuleCodeUpdateChannel = _streamingRuleHub.SubscribeCodeUpdate(applicationLifetime);
        AuditPolicyPreferenceChannel = _streamingRuleHub.SubscribeAuditPolicyPreference(applicationLifetime);
    }
    
    public override Subject<ConsumptionParameters> EventLogProviderObservable => _eventLogProviderSubject;
    public override ChannelReader<RuleHub.RuleCreation> RuleCreationChannel { get; }
    public override ChannelReader<RuleIdContract> RuleEnablementChannel { get; }
    public override ChannelReader<RuleIdContract> RuleDisablementChannel { get; }
    public override ChannelReader<RuleHub.RuleAuditPolicyPreference> AuditPolicyPreferenceChannel { get; }
    protected override ChannelReader<RuleIdContract> RuleDeletionChannel { get; }
    protected override ChannelReader<RuleHub.RuleCodeUpdate> RuleCodeUpdateChannel { get; }

    public override async Task SetAuditPoliciesAsync(RuleHub.AuditPolicyPreference preference, CancellationToken cancellationToken)
    {
        if (DomainHelper.DomainJoined)
        {
            var primaryDomainControllerDnsName = ActiveDirectoryHelper.GetPrimaryDomainControllerDnsName(Logger, DomainHelper.DomainName, cancellationToken);
            if (MachineNameHelper.FullyQualifiedName.Equals(primaryDomainControllerDnsName, StringComparison.OrdinalIgnoreCase))
            {
                DomainAuditPolicyHelper.SetAuditPolicies(GroupPolicyObjectHelper.LocalGpoPath, AuditPolicyAdvanced.GetAuditOptionBySubcategory(GetEventIds().ToHashSet()), overrideAuditPolicies: preference == RuleHub.AuditPolicyPreference.Override, cancellationToken);
                await GroupPolicyHelper.UpdateGroupPoliciesAsync(Logger, cancellationToken);
            }
        }
        else
        {
            await SetLocalPoliciesAsync(preference, cancellationToken);
        }
    }
    
    protected override async Task RestoreAuditPoliciesAsync(CancellationToken cancellationToken)
    {
        if (!DomainHelper.DomainJoined)
        {
            await RestoreLocalAuditPoliciesAsync(cancellationToken);
        }
    }

    protected override RuleHub.AuditPolicyPreference GetAuditPolicyPreference()
    {
        return RuleHub.AuditPolicyPreference.Override; // By default, and because we have no way to know which preference is set by the settings at the start of an agent, use the Override default value
    }

    protected override Task CreateNonBuiltinRulesAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task<CreatedRule> CopyRuleAsync(CopyRule copyRule, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task EnableRulesAsync(IList<EnableRule> enableRules, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task DisableRulesAsync(IList<DisableRule> disableRules, bool commit, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task DeleteRulesAsync(IList<DeleteRule> deleteRules, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
    
    public override Task<UpdatedRule> UpdateRuleCodeAsync(UpdateRuleCode updateRuleCode, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task UpdateAsync(bool includeNonBuiltinRules, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
    
    protected override Task OnLoadedRuleAsync(RuleBase rule, RuleBuiltinType ruleBuiltinType, AuditPolicyVolume auditPolicyVolume, RuleSource ruleSource, bool enabled, string content, string? groupName, CancellationToken cancellationToken)
    {
        try
        {
            _streamingRuleHub.SendRule(rule.ToContract(ruleBuiltinType, auditPolicyVolume, ruleSource, enabled, content, groupName));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error has occurred");
        }

        return Task.CompletedTask;
    }
    
    private async Task SetLocalPoliciesAsync(RuleHub.AuditPolicyPreference auditPolicyPreference, CancellationToken cancellationToken)
    {
        var auditPolicies = await LocalAuditPolicyHelper.BackupAuditPoliciesAsync(Logger, cancellationToken);
        var advancedAuditPoliciesEnabled = LocalAuditPolicyHelper.IsAdvancedPoliciesEnabled(Logger);
        _auditPoliciesRepository.AddBackup(auditPolicies, advancedAuditPoliciesEnabled);
        if (auditPolicyPreference == RuleHub.AuditPolicyPreference.Override)
        {
            Logger.LogInformation("Configuring audit policies...");
            await LocalAuditPolicyHelper.ClearAuditPoliciesAsync(Logger, cancellationToken);
            if (!advancedAuditPoliciesEnabled)
            {
                LocalAuditPolicyHelper.SetAdvancedPoliciesState(Logger, enable: true);
            }

            LocalAuditPolicyHelper.DeleteAuditPolicies(Logger);
            var configuredSubcategories = new HashSet<Guid>();
            foreach (var pair in AuditPolicyAdvanced.GetAuditOptionBySubcategory(GetEventIds().ToHashSet()))
            {
                await LocalAuditPolicyHelper.SetSubCategoryAuditOptionsAsync(Logger, pair.Key, pair.Value, cancellationToken);
                configuredSubcategories.Add(pair.Key);
            }

            var advancedAuditPoliciesByGuid = AuditPolicyAdvanced.QueryAdvancedAuditPolicies().SelectMany(policy => policy.SubCategories.Select(subCategory => subCategory.SubCategoryGuid)).Except(configuredSubcategories);
            foreach (var subCategoryGuid in advancedAuditPoliciesByGuid)
            {
                await LocalAuditPolicyHelper.SetSubCategoryAuditOptionsAsync(Logger, subCategoryGuid, AdvApi32.POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_NONE, cancellationToken);
            }

            LocalAuditPolicyHelper.SetAuditNtlmInDomain(Logger);
            LocalAuditPolicyHelper.SetAuditReceivingNtlmTraffic(Logger);
            LocalAuditPolicyHelper.SetRestrictSendingNtlmTraffic(Logger);
            LocalAuditPolicyHelper.EnableModuleLogging32(Logger);
            LocalAuditPolicyHelper.EnableModuleLogging64(Logger);
            LocalAuditPolicyHelper.SetModuleNames32(Logger);
            LocalAuditPolicyHelper.SetModuleNames64(Logger);
            LocalAuditPolicyHelper.EnableScriptBlockLogging32(Logger);
            LocalAuditPolicyHelper.EnableScriptBlockLogging64(Logger);
            LocalAuditPolicyHelper.EnableProcessCreationIncludeCmdLine(Logger);
            await GroupPolicyHelper.UpdateGroupPoliciesAsync(Logger, cancellationToken);
        }
        else
        {
            await RestoreLocalAuditPoliciesAsync(cancellationToken);
        }
    }
    
    private async Task RestoreLocalAuditPoliciesAsync(CancellationToken cancellationToken)
    {
        if (_auditPoliciesRepository.TryGetBackup(out var backup, out var advancedAuditPoliciesEnabled))
        {
            await LocalAuditPolicyHelper.ClearAuditPoliciesAsync(Logger, cancellationToken);
            LocalAuditPolicyHelper.SetAdvancedPoliciesState(Logger, enable: advancedAuditPoliciesEnabled);
            LocalAuditPolicyHelper.DeleteAuditPolicies(Logger);
            await LocalAuditPolicyHelper.RestoreAuditPoliciesAsync(Logger, backup, cancellationToken);
            await GroupPolicyHelper.UpdateGroupPoliciesAsync(Logger, cancellationToken);
        }
        else
        {
            Logger.LogError("Could not retrieve audit policy backup");
        }
    }
    
    public override void Dispose()
    {
        base.Dispose();
        _eventLogProviderSubject.Dispose();
    }
}