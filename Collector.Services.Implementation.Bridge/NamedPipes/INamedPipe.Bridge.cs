using Shared.Models.Console.Requests;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Implementation.Bridge.NamedPipes;

public interface INamedPipeBridge
{
    Task ExecuteAsync(string domain, string server, CancellationToken cancellationToken);
    ValueTask SendRuleCreationAsync(CreateRule createRule, TaskCompletionSource<RuleCreationResponseContract> response, CancellationToken cancellationToken, params string[] serverNames);
    ValueTask SendRuleEnablementAsync(EnableRule enableRule, CancellationToken cancellationToken, params string[] serverNames);
    ValueTask SendRuleDisablementAsync(DisableRule disableRule, CancellationToken cancellationToken, params string[] serverNames);
    ValueTask SendRuleDeletionAsync(DeleteRule deleteRule, CancellationToken cancellationToken);
    ValueTask SendRuleCodeUpdateAsync(UpdateRuleCode updateRuleCode, TaskCompletionSource<RuleCodeUpdateResponseContract> response, CancellationToken cancellationToken);
    ValueTask SendAuditPolicyPreference(RuleHub.AuditPolicyPreference preference, string[] servers, CancellationToken cancellationToken);
}