using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Channels;
using Collector.Core.EventProviders;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Stores.Rules;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Detection.Rules;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Implementation.Bridge.NamedPipes;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Implementation.Bridge.Rules;

public sealed class RuleServiceBridge : IRuleService
{
    private readonly IDisposable _ruleInsertionSubscription;
    private readonly ILogger<RuleServiceBridge> _logger;
    private readonly INamedPipeBridge _namedPipeBridge;
    private readonly IRuleRepository _ruleRepository;
    private readonly IDetectionRepository _detectionRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IDisposable _onServerConnectedSubscription;
    private readonly IRuleStore _ruleStore;
    private readonly ISettingsStore _settingsStore;

    public RuleServiceBridge(ILogger<RuleServiceBridge> logger, INamedPipeBridge namedPipeBridge, IRuleRepository ruleRepository, IDetectionRepository detectionRepository, ISettingsRepository settingsRepository, ISystemAuditService systemAuditService, IRuleStore ruleStore, ISettingsStore settingsStore)
    {
        _logger = logger;
        _namedPipeBridge = namedPipeBridge;
        _ruleRepository = ruleRepository;
        _detectionRepository = detectionRepository;
        _settingsRepository = settingsRepository;
        _ruleStore = ruleStore;
        _settingsStore = settingsStore;
        _ruleInsertionSubscription = CreateRuleInsertionSubscription();
        _onServerConnectedSubscription = systemAuditService.OnServerConnected.Window(TimeSpan.FromMilliseconds(2500)).SelectMany(o => o.ToArray()).Select(serverNames => Observable.FromAsync(async ct =>
        {
            if (serverNames.Length == 0) return;
            await LoadRulesCoreAsync(serverNames, ct);
        })).Merge().Subscribe();
    }
    
    private IDisposable CreateRuleInsertionSubscription()
    {
        return _ruleRepository.RuleInsertionObservable
            .Select(_ => Observable.FromAsync(ct => Task.WhenAll(EnableRulesAsync(ct), DisableRulesAsync(ct))))
            .Switch()
            .Subscribe();
    }

    private async Task EnableRulesAsync(CancellationToken cancellationToken, params string[] serverNames)
    {
        try
        {
            await foreach (var record in _ruleRepository.EnumerateEnabledRuleIdsAsync(cancellationToken))
            {
                try
                {
                    _ruleStore.Add(record);
                    await _namedPipeBridge.SendRuleEnablementAsync(new EnableRule(record.RuleId), cancellationToken, serverNames);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Cancellation has occurred");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error has occurred");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
    }
    
    private async Task DisableRulesAsync(CancellationToken cancellationToken, params string[] serverNames)
    {
        try
        {
            await foreach (var record in _ruleRepository.EnumerateDisabledRuleIdsAsync(cancellationToken))
            {
                try
                {
                    _ruleStore.Add(record);
                    await _namedPipeBridge.SendRuleDisablementAsync(new DisableRule(record.RuleId), cancellationToken, serverNames);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Cancellation has occurred");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error has occurred");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
    }
    
    private async Task LoadRulesCoreAsync(string[] serverNames, CancellationToken cancellationToken)
    {
        await SendAuditPolicyPreference(serverNames, cancellationToken);
        await Task.WhenAll(EnableRulesAsync(cancellationToken, serverNames), DisableRulesAsync(cancellationToken, serverNames));
        await CreateNonBuiltinRulesAsync(serverNames, cancellationToken);
    }

    private async Task CreateNonBuiltinRulesAsync(string[] serverNames, CancellationToken cancellationToken)
    {
        try
        {
            var tcs = new TaskCompletionSource<RuleCreationResponseContract>(TaskCreationOptions.RunContinuationsAsynchronously);  // We do not await it on purpose
            await foreach (var ruleContent in _ruleRepository.EnumerateNonBuiltinRulesAsync(cancellationToken))
            {
                try
                {
                    await _namedPipeBridge.SendRuleCreationAsync(new CreateRule(ruleContent.Content, ruleContent.Enabled, waitIndefinitely: false, ruleContent.GroupName), tcs, cancellationToken, serverNames);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Cancellation has occurred");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error has occurred");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
    }

    private async Task SendAuditPolicyPreference(string[] servers, CancellationToken cancellationToken)
    {
        await _namedPipeBridge.SendAuditPolicyPreference(_settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride, servers, cancellationToken);
    }
    
    public Task LoadRulesAsync(CancellationToken cancellationToken) => Task.CompletedTask; // This call does not do anything on purpose. Real logic is invoked when domain controller connection is established, through which LoadRulesCoreAsync is called. 

    private Task CreateNonBuiltinRulesAsync(CancellationToken cancellationToken) => CreateNonBuiltinRulesAsync(serverNames: [], cancellationToken);

    public async Task<CreatedRule> CreateRuleAsync(CreateRule createRule, RuleHub.AuditPolicyPreference auditPolicyPreference, bool channelForwarding, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<RuleCreationResponseContract>(TaskCreationOptions.RunContinuationsAsynchronously);
        await _namedPipeBridge.SendRuleCreationAsync(createRule, tcs, cancellationToken);
        var response = await (createRule.WaitIndefinitely ? tcs.Task : await Task.WhenAny(tcs.Task, Timeout()));
        return new CreatedRule(response.Title, response.Id);
        
        async Task<RuleCreationResponseContract> Timeout()
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            throw new Exception("Timed-out after 1 minute");
        }
    }

    public async Task<CreatedRule> CopyRuleAsync(CopyRule copyRule, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<RuleCreationResponseContract>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (content, enabled) = await _ruleRepository.CopyRuleAsync(copyRule.RuleId, copyRule.Title, copyRule.GroupName, cancellationToken);
        await _namedPipeBridge.SendRuleCreationAsync(new CreateRule(content, enabled, waitIndefinitely: false, groupName: copyRule.GroupName), tcs, cancellationToken);
        var response = await await Task.WhenAny(tcs.Task, Timeout());
        return new CreatedRule(response.Title, response.Id);
        
        async Task<RuleCreationResponseContract> Timeout()
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            throw new Exception("Timed-out after 1 minute");
        }
    }

    public async Task EnableRulesAsync(IList<EnableRule> enableRules, CancellationToken cancellationToken)
    {
        await _ruleRepository.EnableAsync(enableRules, cancellationToken);
        foreach (var rule in enableRules)
        {
            await _namedPipeBridge.SendRuleEnablementAsync(rule, cancellationToken);
        }
        
        await _settingsRepository.SetProfileAsync(DetectionProfile.Custom, cancellationToken);
    }

    public async Task DisableRulesAsync(IList<DisableRule> disableRules, bool commit, CancellationToken cancellationToken)
    {
        if (commit)
        {
            await _ruleRepository.DisableAsync(disableRules, cancellationToken);
        }
        
        foreach (var rule in disableRules)
        {
            await _namedPipeBridge.SendRuleDisablementAsync(rule, cancellationToken);
        }

        if (commit)
        {
            await _settingsRepository.SetProfileAsync(DetectionProfile.Custom, cancellationToken);
        }
    }

    public async Task DeleteRulesAsync(IList<DeleteRule> deleteRules, CancellationToken cancellationToken)
    {
        await Task.WhenAll(_ruleRepository.DeleteAsync(deleteRules, cancellationToken), _detectionRepository.DeleteRulesAsync(deleteRules, cancellationToken));
        await _detectionRepository.ComputeMetricsAsync(cancellationToken);
        foreach (var rule in deleteRules)
        {
            await _namedPipeBridge.SendRuleDeletionAsync(rule, cancellationToken);
        }
    }
    
    public async Task<UpdatedRule> UpdateRuleCodeAsync(UpdateRuleCode updateRuleCode, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<RuleCodeUpdateResponseContract>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updatedCode = await _ruleRepository.UpdateRuleCodeAsync(updateRuleCode.RuleId, code: Encoding.UTF8.GetString(updateRuleCode.Code), cancellationToken);
        await _namedPipeBridge.SendRuleCodeUpdateAsync(new UpdateRuleCode(updateRuleCode.RuleId, Encoding.UTF8.GetBytes(updatedCode)), tcs, cancellationToken);

        var response = await await Task.WhenAny(tcs.Task, Timeout());
        return new UpdatedRule(response.HasError ? response.Error : null);
        
        async Task<RuleCodeUpdateResponseContract> Timeout()
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            return new RuleCodeUpdateResponseContract
            {
                Error = "Timed-out after 1 minute"
            };
        }
    }

    public async Task UpdateAsync(bool includeNonBuiltinRules, CancellationToken cancellationToken)
    {
        await SendAuditPolicyPreference(await EnumeratePrimaryDomainControllersAsync(), cancellationToken);
        await _ruleRepository.EnableRulesAsync(cancellationToken);
        await Task.WhenAll(EnableRulesAsync(cancellationToken), DisableRulesAsync(cancellationToken));
        if (includeNonBuiltinRules)
        {
            await CreateNonBuiltinRulesAsync(cancellationToken);
        }

        return;

        async Task<string[]> EnumeratePrimaryDomainControllersAsync()
        {
            var primaryDomainControllers = new HashSet<string>();
            await foreach (var domain in _settingsRepository.EnumerateDomainsAsync(cancellationToken))
            {
                primaryDomainControllers.Add(domain.PrimaryDomainController);
            }
        
            return primaryDomainControllers.ToArray();
        }
    }
    
    public Task SetAuditPoliciesAsync(RuleHub.AuditPolicyPreference preference, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public HashSet<int> GetEventIds()
    {
        throw new NotImplementedException();
    }

    public ConcurrentHashSet<RuleBase> GetRules(int eventId)
    {
        throw new NotImplementedException();
    }

    public EventIdsByProvider GetEventIdsByProvider()
    {
        throw new NotImplementedException();
    }

    public ISet<string> GetProperties(string ruleId)
    {
        throw new NotImplementedException();
    }
    
    public bool TryDisableRuleById(string ruleId)
    {
        throw new NotImplementedException();
    }

    public bool TryEnableRuleById(string ruleId)
    {
        throw new NotImplementedException();
    }

    public Subject<ConsumptionParameters> EventLogProviderObservable => throw new NotImplementedException();
    public ChannelReader<RuleHub.RuleCreation> RuleCreationChannel => throw new NotImplementedException();
    public ChannelReader<RuleIdContract> RuleEnablementChannel => throw new NotImplementedException();
    public ChannelReader<RuleIdContract> RuleDisablementChannel => throw new NotImplementedException();
    public ChannelReader<RuleHub.RuleAuditPolicyPreference> AuditPolicyPreferenceChannel => throw new NotImplementedException();
    public IDictionary<ProviderKey, ISet<int>> DefaultEventIds => throw new NotImplementedException();

    public void Dispose()
    {
        _ruleInsertionSubscription.Dispose();
        _onServerConnectedSubscription.Dispose();
    }
}