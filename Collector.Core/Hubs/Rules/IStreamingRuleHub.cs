using System.Threading.Channels;
using Shared.Streaming.Hubs;
using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.Rules;

public interface IStreamingRuleHub : IRuleForwarder, IDisposable
{
    void SendRule(RuleContract ruleContract);
    void SendRuleUpdate(RuleUpdateContract ruleUpdateContract);
    ChannelReader<RuleHub.RuleCreation> RuleCreationChannel { get; }
    ChannelReader<RuleHub.RuleEnablement> RuleEnablementChannel { get; }
    ChannelReader<RuleHub.RuleDisablement> RuleDisablementChannel { get; }
    ChannelReader<RuleHub.RuleDeletion> RuleDeletionChannel { get; }
    ChannelReader<RuleHub.RuleCodeUpdate> RuleCodeUpdateChannel { get; }
    ChannelReader<RuleHub.RuleAuditPolicyPreference> AuditPolicyPreferenceChannel { get; }
}