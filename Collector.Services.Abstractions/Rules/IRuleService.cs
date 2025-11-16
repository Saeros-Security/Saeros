using System.Reactive.Subjects;
using System.Threading.Channels;
using Collector.Core.EventProviders;
using Collector.Detection.Rules;
using ConcurrentCollections;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Abstractions.Rules;

public interface IRuleService : IDisposable
{
    Task LoadRulesAsync(CancellationToken cancellationToken);
    Task<CreatedRule> CreateRuleAsync(CreateRule createRule, RuleHub.AuditPolicyPreference auditPolicyPreference, bool channelForwarding, CancellationToken cancellationToken);
    Task<CreatedRule> CopyRuleAsync(CopyRule copyRule, CancellationToken cancellationToken);
    Task EnableRulesAsync(IList<EnableRule> enableRules, CancellationToken cancellationToken);
    Task DisableRulesAsync(IList<DisableRule> disableRules, bool commit, CancellationToken cancellationToken);
    Task DeleteRulesAsync(IList<DeleteRule> deleteRules, CancellationToken cancellationToken);
    Task<UpdatedRule> UpdateRuleCodeAsync(UpdateRuleCode updateRuleCode, CancellationToken cancellationToken);
    Task SetAuditPoliciesAsync(RuleHub.AuditPolicyPreference preference, CancellationToken cancellationToken);
    Task UpdateAsync(bool includeNonBuiltinRules, CancellationToken cancellationToken);
    HashSet<int> GetEventIds();
    ConcurrentHashSet<RuleBase> GetRules(int eventId);
    EventIdsByProvider GetEventIdsByProvider();
    ISet<string> GetProperties(string ruleId);
    bool TryDisableRuleById(string ruleId);
    bool TryEnableRuleById(string ruleId);
    Subject<ConsumptionParameters> EventLogProviderObservable { get; }
    ChannelReader<RuleHub.RuleCreation> RuleCreationChannel { get; }
    ChannelReader<RuleIdContract> RuleEnablementChannel { get; }
    ChannelReader<RuleIdContract> RuleDisablementChannel { get; }
    ChannelReader<RuleHub.RuleAuditPolicyPreference> AuditPolicyPreferenceChannel { get; }
    IDictionary<ProviderKey, ISet<int>> DefaultEventIds { get; }
}