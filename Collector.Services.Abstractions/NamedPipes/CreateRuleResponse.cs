using Shared.Models.Console.Requests;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Abstractions.NamedPipes;

public sealed record CreateRuleResponse(CreateRule CreateRule, RuleHub.AuditPolicyPreference Preference, TaskCompletionSource<RuleCreationResponseContract> Response);
