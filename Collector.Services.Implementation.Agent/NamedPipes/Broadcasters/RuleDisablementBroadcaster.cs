using System.Threading.Channels;
using Collector.Core.Hubs.Rules;
using Collector.Services.Implementation.Agent.Helpers;
using ConcurrentCollections;
using Microsoft.Extensions.Hosting;
using Streaming;

namespace Collector.Services.Implementation.Agent.NamedPipes.Broadcasters;

public static class RuleDisablementBroadcaster
{
    private static readonly ConcurrentHashSet<Channel<RuleIdContract>> Channels = new();
    private static readonly ConcurrentCircularBuffer<RuleIdContract> Contracts = new(capacity: 10_000);
    private static int _initialized;
    
    public static void RemoveDisablement(this Channel<RuleIdContract> channel) => Channels.TryRemove(channel);

    public static Channel<RuleIdContract> SubscribeDisablement(this IStreamingRuleHub hub, IHostApplicationLifetime applicationLifetime)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, comparand: 0) == 0)
        {
            _ = BroadcastAsync(hub, applicationLifetime.ApplicationStopping);
        }
        
        var channel = Channel.CreateUnbounded<RuleIdContract>();
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
            await foreach (var message in hub.RuleDisablementChannel.ReadAllAsync(cancellationToken))
            {
                Contracts.Enqueue(message.RuleId);
                foreach (var channel in Channels)
                {
                    channel.Writer.TryWrite(message.RuleId);
                }
            }
        }
        catch (OperationCanceledException)
        {

        }
    }
}