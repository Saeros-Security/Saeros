using System.Threading.Channels;
using Collector.Core.Hubs.Rules;
using Collector.Services.Implementation.Agent.Helpers;
using ConcurrentCollections;
using Microsoft.Extensions.Hosting;
using Shared.Streaming.Hubs;

namespace Collector.Services.Implementation.Agent.NamedPipes.Broadcasters;

public static class RuleAuditPoliciesBroadcaster
{
    private static readonly ConcurrentHashSet<Channel<RuleHub.RuleAuditPolicyPreference>> Channels = new();
    private static readonly ConcurrentCircularBuffer<RuleHub.RuleAuditPolicyPreference> Contracts = new(capacity: 1);
    private static int _initialized;
    
    public static Channel<RuleHub.RuleAuditPolicyPreference> SubscribeAuditPolicyPreference(this IStreamingRuleHub hub, IHostApplicationLifetime applicationLifetime)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, comparand: 0) == 0)
        {
            _ = BroadcastAsync(hub, applicationLifetime.ApplicationStopping);
        }
        
        var channel = Channel.CreateUnbounded<RuleHub.RuleAuditPolicyPreference>();
        Channels.Add(channel);
        
        // Replay already received messages
        foreach (var contract in Contracts)
        {
            channel.Writer.TryWrite(contract);
        }
        
        return channel;
    }

    private static async Task BroadcastAsync(IStreamingRuleHub hub, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in hub.AuditPolicyPreferenceChannel.ReadAllAsync(cancellationToken))
            {
                Contracts.Enqueue(message);
                foreach (var channel in Channels)
                {
                    channel.Writer.TryWrite(message);
                }
            }
        }
        catch (OperationCanceledException)
        {

        }
    }
}