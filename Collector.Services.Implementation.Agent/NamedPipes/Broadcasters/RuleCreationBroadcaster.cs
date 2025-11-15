using System.Threading.Channels;
using Collector.Core.Hubs.Rules;
using Collector.Services.Implementation.Agent.Helpers;
using ConcurrentCollections;
using Microsoft.Extensions.Hosting;
using Shared.Streaming.Hubs;

namespace Collector.Services.Implementation.Agent.NamedPipes.Broadcasters;

public static class RuleCreationBroadcaster
{
    private static readonly ConcurrentHashSet<Channel<RuleHub.RuleCreation>> Channels = new();
    private static readonly ConcurrentCircularBuffer<RuleHub.RuleCreation> Contracts = new(capacity: 10_000);
    private static int _initialized;
    
    public static void Remove(this Channel<RuleHub.RuleCreation> channel) => Channels.TryRemove(channel);

    public static Channel<RuleHub.RuleCreation> SubscribeCreation(this IStreamingRuleHub hub, IHostApplicationLifetime applicationLifetime)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, comparand: 0) == 0)
        {
            _ = BroadcastAsync(hub, applicationLifetime.ApplicationStopping);
        }
        
        var channel = Channel.CreateUnbounded<RuleHub.RuleCreation>();
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
            await foreach (var message in hub.RuleCreationChannel.ReadAllAsync(cancellationToken))
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