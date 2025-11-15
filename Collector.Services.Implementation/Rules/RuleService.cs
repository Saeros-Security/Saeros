using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Channels;
using Collector.ActiveDirectory.Helpers;
using Collector.Core;
using Collector.Core.EventProviders;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Databases.Abstractions.Repositories.RuleConfigurations;
using Collector.Detection.Contracts;
using Collector.Detection.Converters;
using Collector.Detection.Mitre;
using Collector.Detection.Rules;
using Collector.Detection.Rules.Extensions;
using Collector.Detection.Rules.Helpers;
using Collector.Detection.Rules.Serializers;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Implementation.Rules.Helpers;
using ConcurrentCollections;
using Detection.Helpers;
using Detection.Yaml;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Extensions;
using Shared.Helpers;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Models.Detections;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Implementation.Rules;

public abstract class RuleService(ILogger<RuleService> logger, IEventProviderServiceReader eventProviderService, IRuleConfigurationsRepository ruleConfigurationsRepository)
    : IRuleService
{
    private const bool SysmonInstalled = false;

    private sealed record AddRuleResult(bool Added, string Title, string Id, string? FailureReason);

    private class GroupName(string name)
    {
        public string Name { get; } = name;
        public static readonly Empty Empty = new();
        public static readonly Tactic Tactic = new();
    }

    private sealed class Empty() : GroupName(string.Empty);
    private sealed class Tactic() : GroupName(string.Empty);
    
    private static readonly ConcurrentHashSet<RuleBase> EmptyRules = new();
    private readonly EventIdsByProvider _eventIdsByProviderKey = new(new Dictionary<ProviderKey, ISet<int>>(),new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    private readonly ConcurrentDictionary<int, ConcurrentHashSet<RuleBase>> _rulesByEventId = new();
    private readonly ConcurrentDictionary<string, ISet<string>> _propertiesByRuleId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuleBase> _ruleByRuleId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuleBuiltinType> _ruleTypeByRuleId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuleBuiltinType> _ruleTypeByRuleTitle = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentHashSet<string> _ruleTitles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentHashSet<string> _disabledRuleIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, ISet<int>> _eventIdsByRuleId = new Dictionary<string, ISet<int>>();
    private readonly ConcurrentDictionary<string, ISet<string>> _primaryDomainControllers = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<ProviderKey, ISet<int>> _defaultEventIds = new()
    {
        { new ProviderKey(new Guid("54849625-5478-4994-A5BA-3E3B0328C30D"), providerName: "Microsoft-Windows-Security-Auditing", ProviderType.Manifest), new HashSet<int> { 4624, 4625, 4688, 4689 } } // Required for tracing
    };

    protected readonly ILogger<RuleService> Logger = logger;

    private IDisposable? _deletionRuleSubscription;
    private IDisposable? _ruleCodeUpdateSubscription;

    private ISet<string> GetDomainControllers(CancellationToken cancellationToken)
    {
        return _primaryDomainControllers.GetOrAdd(string.Empty, valueFactory: _ => GetDomainControllerCore(cancellationToken));
    }

    private ISet<string> GetDomainControllerCore(CancellationToken cancellationToken)
    {
        if (DomainHelper.DomainJoined)
        {
            return ActiveDirectoryHelper.EnumerateDomainControllers(Logger, DomainHelper.DomainName, cancellationToken).Select(dc => dc.serverName).ToHashSet();
        }

        return new HashSet<string>();
    }
    
    private void DeleteRule(RuleIdContract ruleIdContract, ConsumptionParameters providerParameters)
    {
        if (_ruleByRuleId.TryRemove(ruleIdContract.RuleId, out var removedRule))
        {
            _eventIdsByRuleId.Remove(removedRule.Id, out _);
            _propertiesByRuleId.TryRemove(removedRule.Id, out _);
            _ruleTypeByRuleId.TryRemove(removedRule.Id, out _);
            _ruleTypeByRuleTitle.TryRemove(removedRule.Id, out _);
            foreach (var kvp in _rulesByEventId)
            {
                kvp.Value.TryRemove(removedRule);
            }

            _ruleTitles.TryRemove(removedRule.Metadata.Title);
            if (providerParameters.ProviderChanged)
            {
                EventLogProviderObservable.OnNext(providerParameters);
            }
        }
    }

    private async Task UpdateRuleCodeAsync(RuleHub.RuleCodeUpdate ruleCodeUpdate, CancellationToken cancellationToken)
    {
        try
        {
            if (ruleConfigurationsRepository.TryGetAliases(out var aliases) &&
                ruleConfigurationsRepository.TryGetDetails(out var details) &&
                ruleConfigurationsRepository.TryGetChannelAbbrevations(out var channelAbbreviations) &&
                ruleConfigurationsRepository.TryGetProviderAbbrevations(out var providerAbbreviations))
            {
                var yaml = ruleCodeUpdate.CodeUpdate.Code.RemoveBom();
                if (!RuleSerializer.TryDeserialize(Logger, yaml, _ => true, aliases, details, channelAbbreviations, providerAbbreviations, domainControllers: GetDomainControllers(cancellationToken), out var rule, out _, out _, out _, out var error))
                {
                    ruleCodeUpdate.Response.TrySetResult(new RuleCodeUpdateResponseContract { Error = $"Could not parse rule code: {error}" });
                    return;
                }

                DeleteRule(new RuleIdContract { RuleId = rule.Id, AuditPolicyPreference = ruleCodeUpdate.CodeUpdate.AuditPolicyPreference }, new ConsumptionParameters((RuleHub.AuditPolicyPreference)ruleCodeUpdate.CodeUpdate.AuditPolicyPreference, ProviderChanged: false));
                var result = await AddRuleCoreAsync(filter: _ => true, enabled: _ => true, RuleBuiltinType.NonBuiltin, groupName: GroupName.Empty, yaml, aliases, details, channelAbbreviations, providerAbbreviations, (RuleHub.AuditPolicyPreference)ruleCodeUpdate.CodeUpdate.AuditPolicyPreference, cancellationToken);
                if (!result.Added)
                {
                    ruleCodeUpdate.Response.TrySetResult(new RuleCodeUpdateResponseContract { Error = result.FailureReason });
                    EventLogProviderObservable.OnNext(new ConsumptionParameters((RuleHub.AuditPolicyPreference)ruleCodeUpdate.CodeUpdate.AuditPolicyPreference, ProviderChanged: true));
                    return;
                }

                EventLogProviderObservable.OnNext(new ConsumptionParameters((RuleHub.AuditPolicyPreference)ruleCodeUpdate.CodeUpdate.AuditPolicyPreference, ProviderChanged: true));
                ruleCodeUpdate.Response.TrySetResult(new RuleCodeUpdateResponseContract());
            }
            else
            {
                ruleCodeUpdate.Response.TrySetResult(new RuleCodeUpdateResponseContract { Error = "Could not load rule configurations" });
            }
        }
        catch (Exception ex)
        {
            ruleCodeUpdate.Response.TrySetResult(new RuleCodeUpdateResponseContract { Error = ex.Message });
        }
    }
    
    public async Task LoadRulesAsync(CancellationToken cancellationToken)
    {
        ruleConfigurationsRepository.AddAliases(ConfigHelper.GetAliases());
        ruleConfigurationsRepository.AddDetails(ConfigHelper.GetDetails());
        ruleConfigurationsRepository.AddChannelAbbrevations(ConfigHelper.GetChannelAbbreviations());
        ruleConfigurationsRepository.AddProviderAbbrevations(ConfigHelper.GetProviderAbbreviations());
        ruleConfigurationsRepository.AddExcludedRules(ConfigHelper.GetExcludedRules());
        ruleConfigurationsRepository.AddNoisyRules(ConfigHelper.GetNoisyRules());
        ruleConfigurationsRepository.AddProvenRules(ConfigHelper.GetProvenRules());
        
        if (ruleConfigurationsRepository.TryGetExcludedRules(out var excludedRules) &&
            ruleConfigurationsRepository.TryGetNoisyRules(out var noisyRules) &&
            ruleConfigurationsRepository.TryGetAliases(out var aliases) &&
            ruleConfigurationsRepository.TryGetDetails(out var details) &&
            ruleConfigurationsRepository.TryGetChannelAbbrevations(out var channelAbbreviations) &&
            ruleConfigurationsRepository.TryGetProviderAbbrevations(out var providerAbbreviations))
        {
            await LoadRulesAsync(excludedRules, noisyRules, aliases, details, channelAbbreviations, providerAbbreviations, GetAuditPolicyPreference(), cancellationToken);
            _deletionRuleSubscription = RuleDeletionChannel.ReadAllAsync(CancellationToken.None).ToObservable().Do(ruleDeletion => DeleteRule(ruleDeletion, new ConsumptionParameters((RuleHub.AuditPolicyPreference)ruleDeletion.AuditPolicyPreference, ProviderChanged: true))).Subscribe();
            _ruleCodeUpdateSubscription = RuleCodeUpdateChannel.ReadAllAsync(CancellationToken.None).ToObservable().Select(update => Observable.FromAsync(ct => UpdateRuleCodeAsync(update, ct))).Concat().Subscribe();
        }
    }
    
    public HashSet<int> GetEventIds()
    {
        return _rulesByEventId.Where(kvp => kvp.Value.Select(rule => rule.Id).Except(_disabledRuleIds).Any()).Select(kvp => kvp.Key).Concat(_defaultEventIds.Values.SelectMany(eventIds => eventIds)).ToHashSet();
    }

    public ConcurrentHashSet<RuleBase> GetRules(int eventId)
    {
        return _rulesByEventId.GetValueOrDefault(eventId, EmptyRules);
    }

    public EventIdsByProvider GetEventIdsByProvider()
    {
        return _eventIdsByProviderKey;
    }

    public ISet<string> GetProperties(string ruleId)
    {
        if (_propertiesByRuleId.TryGetValue(ruleId, out var properties))
        {
            return properties;
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public bool TryEnableRuleById(string ruleId)
    {
        return _disabledRuleIds.TryRemove(ruleId);
    }
    
    public bool TryDisableRuleById(string ruleId)
    {
        return _disabledRuleIds.Add(ruleId);
    }

    public virtual async Task<CreatedRule> CreateRuleAsync(CreateRule createRule, RuleHub.AuditPolicyPreference auditPolicyPreference, bool channelForwarding, CancellationToken cancellationToken)
    {
        if (ruleConfigurationsRepository.TryGetAliases(out var aliases) &&
            ruleConfigurationsRepository.TryGetDetails(out var details) &&
            ruleConfigurationsRepository.TryGetChannelAbbrevations(out var channelAbbreviations) &&
            ruleConfigurationsRepository.TryGetProviderAbbrevations(out var providerAbbreviations))
        {
            var yamlString = Encoding.UTF8.GetString(createRule.Rule.AsSpan().RemoveBom());
            if (SigmaRuleConverter.TryConvertSigmaRule(Logger, yamlString, SysmonInstalled, out var convertedRule, out var error))
            {
                var (added, title, id, failureReason) = await AddRuleCoreAsync(filter: _ => true, enabled: _ => createRule.Enabled, RuleBuiltinType.NonBuiltin, new GroupName(createRule.GroupName), convertedRule, aliases, details, channelAbbreviations, providerAbbreviations, auditPolicyPreference, cancellationToken);
                if (!added)
                {
                    throw new Exception(!string.IsNullOrWhiteSpace(failureReason) ? failureReason : "Could not add rule.");
                }

                EventLogProviderObservable.OnNext(new ConsumptionParameters(auditPolicyPreference, ProviderChanged: true));
                return new CreatedRule(title, id);
            }
            else
            {
                throw new Exception($"Could not convert Sigma rule: {error}.");
            }
        }

        throw new Exception("Rule configurations are missing.");
    }

    public IDictionary<ProviderKey, ISet<int>> DefaultEventIds => _defaultEventIds;

    public abstract Task<CreatedRule> CopyRuleAsync(CopyRule copyRule, CancellationToken cancellationToken);
    public abstract Task EnableRulesAsync(IList<EnableRule> enableRules, CancellationToken cancellationToken);
    public abstract Task DisableRulesAsync(IList<DisableRule> disableRules, bool commit, CancellationToken cancellationToken);
    public abstract Task DeleteRulesAsync(IList<DeleteRule> deleteRules, CancellationToken cancellationToken);
    public abstract Task<UpdatedRule> UpdateRuleCodeAsync(UpdateRuleCode updateRuleCode, CancellationToken cancellationToken);
    public abstract Task SetAuditPoliciesAsync(RuleHub.AuditPolicyPreference preference, CancellationToken cancellationToken);
    public abstract Task UpdateAsync(bool includeNonBuiltinRules, CancellationToken cancellationToken);
    public abstract Subject<ConsumptionParameters> EventLogProviderObservable { get; }
    public abstract ChannelReader<RuleHub.RuleCreation> RuleCreationChannel { get; }
    public abstract ChannelReader<RuleIdContract> RuleEnablementChannel { get; }
    public abstract ChannelReader<RuleIdContract> RuleDisablementChannel { get; }
    public abstract ChannelReader<RuleHub.RuleAuditPolicyPreference> AuditPolicyPreferenceChannel { get; }
    protected abstract ChannelReader<RuleIdContract> RuleDeletionChannel { get; }
    protected abstract ChannelReader<RuleHub.RuleCodeUpdate> RuleCodeUpdateChannel { get; }
    protected abstract RuleHub.AuditPolicyPreference GetAuditPolicyPreference();
    protected abstract Task RestoreAuditPoliciesAsync(CancellationToken cancellationToken);
    protected abstract Task CreateNonBuiltinRulesAsync(CancellationToken cancellationToken);
    protected abstract Task OnLoadedRuleAsync(RuleBase rule, RuleBuiltinType ruleBuiltinType, AuditPolicyVolume auditPolicyVolume, RuleSource ruleSource, bool enabled, string content, string? groupName, CancellationToken cancellationToken);
    
    protected virtual async Task LoadRulesAsync(ExcludedRules excludedRules, NoisyRules noisyRules, Aliases aliases, Details details, ChannelAbbrevations channelAbbreviations, ProviderAbbrevations providerAbbreviations, RuleHub.AuditPolicyPreference auditPolicyPreference, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Loading rules...");
        var ruleCount = 0;
        var filter = await BuildFilterAsync();
        await foreach (var yamlString in RuleHelper.EnumerateSigmaBuiltinRules(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (added, _, _, _) = await AddRuleCoreAsync(filter, enabled: IsRuleEnabled, RuleBuiltinType.Builtin, groupName: GroupName.Tactic, yamlString, aliases, details, channelAbbreviations, providerAbbreviations, auditPolicyPreference, cancellationToken);
            if (added)
            {
                ruleCount++;
            }
        }

        Logger.LogInformation("Loaded {Count} rules", ruleCount);
        return;

        async Task<Predicate<RuleMetadata>> BuildFilterAsync()
        {
            var ruleIdsByTitle = new ConcurrentDictionary<string, RuleId>(StringComparer.OrdinalIgnoreCase);
            await foreach (var yamlString in RuleHelper.EnumerateSigmaBuiltinRules(cancellationToken))
            {
                var yamlRules = YamlParser.DeserializeMany<YamlRule>(yamlString).ToList();
                var ruleMetadata = yamlRules.ToMetadata();
                var useSysmon = ruleMetadata.Tags.Contains("sysmon", StringComparer.OrdinalIgnoreCase);
                if (!SysmonInstalled && useSysmon) continue;
                var ruleId = new RuleId(ruleMetadata.Id, useSysmon: useSysmon);
                ruleIdsByTitle.AddOrUpdate(ruleMetadata.Title, addValue: ruleId, updateValueFactory: (_, current) =>
                {
                    if (SysmonInstalled && useSysmon && !current.UseSysmon)
                    {
                        return ruleId;
                    }

                    return current;
                });
            }

            return FilterRule;

            bool FilterRule(RuleMetadata metadata)
            {
                if (string.IsNullOrWhiteSpace(metadata.Description)) return false;
                if (!ruleIdsByTitle.TryGetValue(metadata.Title, out var ruleId) || !ruleId.Id.Equals(metadata.Id, StringComparison.OrdinalIgnoreCase)) return false;
                var status = metadata.Status.FromStatus();
                if (status is DetectionStatus.Unsupported or DetectionStatus.Deprecated) return false;
                return true;
            }
        }
        
        bool IsRuleEnabled(RuleMetadata metadata)
        {
            if (excludedRules.Items.Contains(metadata.Id)) return false;
            if (noisyRules.Items.Contains(metadata.Id)) return false;
            return true;
        }
    }
    
    private async Task<AddRuleResult> AddRuleCoreAsync(Predicate<RuleMetadata> filter, Predicate<RuleMetadata> enabled, RuleBuiltinType ruleBuiltinType, GroupName groupName, string yamlString, Aliases aliases, Details details, ChannelAbbrevations channelAbbreviations, ProviderAbbrevations providerAbbreviations, RuleHub.AuditPolicyPreference auditPolicyPreference, CancellationToken cancellationToken)
    {
        if (!RuleSerializer.TryDeserialize(Logger, yamlString, filter, aliases, details, channelAbbreviations, providerAbbreviations, domainControllers: GetDomainControllers(cancellationToken), out var rule, out var channelEventIds, out var providerEventIds, out var properties, out var error)) return new AddRuleResult(Added: false, Title: string.Empty, Id: string.Empty, FailureReason: $"Could not deserialize rule: {error}.");
        var channelNames = channelEventIds.GroupBy(value => value.Channel).Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Select(value => value.EventId).Where(value => !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit)).Select(int.Parse).ToHashSet());
        var providerNames = providerEventIds.GroupBy(value => value.Provider).Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Select(value => value.EventId).Where(value => !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit)).Select(int.Parse).ToHashSet());
        var eventIdsByProvider = new ConcurrentDictionary<ProviderKey, ISet<int>>();
        if (channelNames.Count == 0 && providerNames.Count == 0)
        {
            Logger.LogWarning("The rule {Rule} targets all providers thus has been excluded due to volume concerns", rule.Id);
            return new AddRuleResult(Added: false, Title: string.Empty, Id: string.Empty, FailureReason: "The rule targets all providers thus has been excluded due to volume concerns.");
        }

        foreach (var pair in channelNames)
        {
            if (!Add(pair, eventIdsByProvider))
            {
                Logger.LogWarning("The channel {Channel} was not found", pair.Key);
            }
        }

        foreach (var pair in providerNames)
        {
            if (!Add(pair, eventIdsByProvider))
            {
                Logger.LogWarning("The provider {Provider} was not found", pair.Key);
            }
        }

        if (eventIdsByProvider.Count == 0)
        {
            Logger.LogWarning("No provider is available for rule {Rule}", rule.Id);
            return new AddRuleResult(Added: false, Title: string.Empty, Id: string.Empty, FailureReason: "No provider is available for this rule.");
        }

        var eventIds = channelEventIds.Select(channelEventId => channelEventId.EventId).Concat(providerEventIds.Select(providerEventId => providerEventId.EventId)).Where(eventId => !string.IsNullOrWhiteSpace(eventId)).Select(int.Parse).ToHashSet();
        if (!Index(rule, auditPolicyPreference, new EventIdsByProvider(eventIdsByProvider, properties), eventIds, ruleBuiltinType))
        {
            return new AddRuleResult(Added: false, rule.Metadata.Title, rule.Metadata.Id, FailureReason: "A built-in rule with same Id or Title is already registered.");
        }
        
        await OnLoadedRuleAsync(rule, ruleBuiltinType, RuleVolumeHelper.ToVolume(rule.Metadata, eventIds), RuleSourceHelper.ToSource(), enabled: enabled(rule.Metadata), content: yamlString, GetGroupName(rule.Metadata, groupName), cancellationToken);
        return new AddRuleResult(Added: true, rule.Metadata.Title, rule.Metadata.Id, FailureReason: null);
    }

    private static string? GetGroupName(RuleMetadata metadata, GroupName groupName)
    {
        if (!string.IsNullOrWhiteSpace(groupName.Name)) return groupName.Name;
        if (groupName is Tactic)
        {
            var mitres = MitreAttackResolver.GetComponents(metadata.Tags);
            return mitres.FirstOrDefault()?.Tactic;
        }
        
        return null;
    }

    private bool Index(RuleBase rule, RuleHub.AuditPolicyPreference auditPolicyPreference, EventIdsByProvider eventIdsByProvider, ISet<int> eventIds, RuleBuiltinType ruleBuiltinType)
    {
        if (!_ruleTitles.Add(rule.Metadata.Title) || _ruleByRuleId.ContainsKey(rule.Metadata.Id))
        {
            if (_ruleTypeByRuleId.TryGetValue(rule.Metadata.Id, out var type) || _ruleTypeByRuleTitle.TryGetValue(rule.Metadata.Title, out type))
            {
                if (type == RuleBuiltinType.Builtin) return false;
            }

            DeleteRule(new RuleIdContract
            {
                RuleId = rule.Id,
                AuditPolicyPreference = (int)auditPolicyPreference
            }, new ConsumptionParameters(auditPolicyPreference, ProviderChanged: false));
        }
        
        _ruleByRuleId[rule.Id] = rule;
        _ruleTypeByRuleId[rule.Id] = ruleBuiltinType;
        _ruleTypeByRuleTitle[rule.Metadata.Title] = ruleBuiltinType;
        _propertiesByRuleId[rule.Id] = eventIdsByProvider.Properties;
        _eventIdsByRuleId[rule.Id] = eventIds;
        foreach (var pair in eventIdsByProvider.Items)
        {
            if (_eventIdsByProviderKey.Items.TryGetValue(pair.Key, out var ids))
            {
                foreach (var eventId in pair.Value)
                {
                    ids.Add(eventId);
                }

                foreach (var eventId in eventIds)
                {
                    ids.Add(eventId);
                }
            }
            else
            {
                _eventIdsByProviderKey.Items.Add(pair.Key, pair.Value.Concat(eventIds).ToHashSet());
            }
        }

        foreach (var eventId in eventIdsByProvider.Items.Values.SelectMany(value => value))
        {
            if (_rulesByEventId.TryGetValue(eventId, out var rules))
            {
                rules.Add(rule);
            }
            else
            {
                _rulesByEventId.TryAdd(eventId, [rule]);
            }
        }

        foreach (var eventId in eventIds)
        {
            if (_rulesByEventId.TryGetValue(eventId, out var rules))
            {
                rules.Add(rule);
            }
            else
            {
                _rulesByEventId.TryAdd(eventId, [rule]);
            }
        }

        return true;
    }

    private bool Add(KeyValuePair<string, HashSet<int>> pair, ConcurrentDictionary<ProviderKey, ISet<int>> eventIdsByProviderKey)
    {
        if (eventProviderService.TryResolveProvider(pair.Key, pair.Value, out var providerGuid, out var providerName, out var providerType, out var userTrace))
        {
            eventIdsByProviderKey.AddOrUpdate(new ProviderKey(providerGuid, providerName, providerType, userTrace), addValue: pair.Value, updateValueFactory: (_, current) =>
            {
                foreach (var value in pair.Value)
                {
                    current.Add(value);
                }

                return current;
            });

            return true;
        }

        return false;
    }
    
    public virtual void Dispose()
    {
        _deletionRuleSubscription?.Dispose();
        _ruleCodeUpdateSubscription?.Dispose();
    }
}