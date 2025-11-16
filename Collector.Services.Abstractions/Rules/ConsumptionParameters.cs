using Shared.Streaming.Hubs;

namespace Collector.Services.Abstractions.Rules;

public sealed record ConsumptionParameters(RuleHub.AuditPolicyPreference AuditPolicyPreference, bool ProviderChanged);
