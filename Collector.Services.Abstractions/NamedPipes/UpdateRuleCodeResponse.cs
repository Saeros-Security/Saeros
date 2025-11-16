using Shared.Models.Console.Requests;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Abstractions.NamedPipes;

public sealed record UpdateRuleCodeResponse(UpdateRuleCode UpdateRuleCode, RuleHub.AuditPolicyPreference Preference, TaskCompletionSource<RuleCodeUpdateResponseContract> Response);