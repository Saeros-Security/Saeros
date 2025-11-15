using System.Threading.Channels;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Abstractions.NamedPipes;

public sealed record Channels(Channel<CreateRuleResponse> RuleCreationChannel, Channel<RuleIdContract> RuleEnablementChannel, Channel<RuleIdContract> RuleDisablementChannel, Channel<RuleIdContract> RuleDeletionChannel, Channel<UpdateRuleCodeResponse> RuleCodeUpdateChannel, Channel<RuleHub.AuditPolicyPreference> AuditPolicyPreferenceChannel);