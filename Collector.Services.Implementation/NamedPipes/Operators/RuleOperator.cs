using System.Text;
using System.Threading.Channels;
using Collector.Core.Extensions;
using Collector.Services.Abstractions.NamedPipes;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Services.Implementation.NamedPipes.Operators;

internal static class RuleOperator
{
    public static async Task StreamRulesAsync(RuleRpcService.RuleRpcServiceClient client, Func<RuleContract, CancellationToken, ValueTask> action, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.StreamRules(new Empty(), cancellationToken: cancellationToken);
            await foreach (var rule in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await action(rule, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
    
    public static async Task StreamRuleUpdatesAsync(RuleRpcService.RuleRpcServiceClient client, Func<RuleUpdateContract, CancellationToken, ValueTask> action, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            using var call = client.UpdateRules(new Empty(), cancellationToken: cancellationToken);
            await foreach (var ruleUpdate in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await action(ruleUpdate, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }

    public static async Task StreamRuleCreationAsync(RuleRpcService.RuleRpcServiceClient client, ChannelReader<CreateRuleResponse> channel, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var ruleCreation in channel.ReadAllAsync(cancellationToken))
            {
                var contract = new RuleCreationContract
                {
                    Content = ByteString.CopyFrom(ruleCreation.CreateRule.Rule),
                    Enabled = ruleCreation.CreateRule.Enabled,
                    WaitIndefinitely = ruleCreation.CreateRule.WaitIndefinitely,
                    GroupName = ruleCreation.CreateRule.GroupName,
                    AuditPolicyPreference = (int)ruleCreation.Preference
                };

                using var call = client.CreateRuleAsync(contract, cancellationToken: cancellationToken);
                var response = await call.ResponseAsync;
                if (response.HasError)
                {
                    ruleCreation.Response.TrySetException(new Exception(response.Error));
                    return;
                }

                ruleCreation.Response.TrySetResult(new RuleCreationResponseContract { Title = response.Title });
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
    
    public static async Task StreamRuleEnablementAsync(RuleRpcService.RuleRpcServiceClient client, ChannelReader<RuleIdContract> channel, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in channel.ReadAllAsync(cancellationToken))
            {
                using var call = client.EnableRuleAsync(item, cancellationToken: cancellationToken);
                await call.ResponseAsync;
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
    
    public static async Task StreamRuleDisablementAsync(RuleRpcService.RuleRpcServiceClient client, ChannelReader<RuleIdContract> channel, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in channel.ReadAllAsync(cancellationToken))
            {
                using var call = client.DisableRuleAsync(item, cancellationToken: cancellationToken);
                await call.ResponseAsync;
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
    
    public static async Task StreamRuleDeletionAsync(RuleRpcService.RuleRpcServiceClient client, ChannelReader<RuleIdContract> channel, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in channel.ReadAllAsync(cancellationToken))
            {
                using var call = client.DeleteRuleAsync(item, cancellationToken: cancellationToken);
                await call.ResponseAsync;
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
    
    public static async Task StreamRuleCodeUpdateAsync(RuleRpcService.RuleRpcServiceClient client, ChannelReader<UpdateRuleCodeResponse> channel, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in channel.ReadAllAsync(cancellationToken))
            {
                using var call = client.UpdateRuleCodeAsync(new RuleCodeUpdateContract { Id = item.UpdateRuleCode.RuleId, Code = Encoding.UTF8.GetString(item.UpdateRuleCode.Code.AsSpan().RemoveBom()), AuditPolicyPreference = (int)item.Preference }, cancellationToken: cancellationToken);
                item.Response.TrySetResult(await call.ResponseAsync);
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
    
    public static async Task StreamAuditPolicyPreferencesAsync(RuleRpcService.RuleRpcServiceClient client, ChannelReader<RuleHub.AuditPolicyPreference> channel, Action<Exception> onCallException, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in channel.ReadAllAsync(cancellationToken))
            {
                using var call = client.UpdateAuditPolicyPreferenceAsync(new Int32Value { Value = (int)item }, cancellationToken: cancellationToken);
                await call.ResponseAsync;
            }
        }
        catch (Exception ex)
        {
            onCallException(ex);
        }
    }
}