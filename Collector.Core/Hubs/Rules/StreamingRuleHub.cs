using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using Collector.Core.Extensions;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Streaming.Hubs;
using Streaming;

namespace Collector.Core.Hubs.Rules;

public sealed class StreamingRuleHub : IStreamingRuleHub
{
    private readonly IDisposable _subscription;
    private readonly Channel<RuleHub.RuleCreation> _ruleCreationChannel = Channel.CreateUnbounded<RuleHub.RuleCreation>();
    private readonly Channel<RuleHub.RuleEnablement> _ruleEnablementChannel = Channel.CreateUnbounded<RuleHub.RuleEnablement>();
    private readonly Channel<RuleHub.RuleDisablement> _ruleDisablementChannel = Channel.CreateUnbounded<RuleHub.RuleDisablement>();
    private readonly Channel<RuleHub.RuleDeletion> _ruleDeletionChannel = Channel.CreateUnbounded<RuleHub.RuleDeletion>();
    private readonly Channel<RuleHub.RuleCodeUpdate> _ruleCodeUpdateChannel = Channel.CreateUnbounded<RuleHub.RuleCodeUpdate>();
    private readonly Channel<RuleHub.RuleAuditPolicyPreference> _auditPolicyPreferenceChannel = Channel.CreateUnbounded<RuleHub.RuleAuditPolicyPreference>();
    private readonly Subject<RuleHub.RuleCreation> _ruleCreationSubject = new();
    private readonly Subject<RuleHub.RuleEnablement> _ruleEnablementSubject = new();
    private readonly Subject<RuleHub.RuleDisablement> _ruleDisablementSubject = new();
    private readonly Subject<RuleHub.RuleDeletion> _ruleDeletionSubject = new();
    private readonly Subject<RuleHub.RuleCodeUpdate> _ruleCodeUpdateSubject = new();
    private readonly Subject<RuleHub.RuleAuditPolicyPreference> _auditPolicyPreferenceSubject = new();

    public StreamingRuleHub(ILogger<StreamingRuleHub> logger, CollectorMode collectorMode)
    {
        _subscription = new CompositeDisposable(SubscribeToRuleCreation(), SubscribeToRuleEnablement(), SubscribeToRuleDisablement(), SubscribeToRuleDeletion(), SubscribeToRuleCodeUpdate(), SubscribeToAuditPolicyPreference());
        
        RuleChannel = collectorMode == CollectorMode.Agent
            ? Channel.CreateBounded<RuleContract>(new BoundedChannelOptions(capacity: 10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            }, itemDropped: _ => logger.Throttle(nameof(StreamingRuleHub), itself => itself.LogWarning("A rule contract has been lost from its channel"), expiration: TimeSpan.FromMinutes(1)))
            : Channel.CreateBounded<RuleContract>(new BoundedChannelOptions(capacity: 1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        RuleUpdateChannel = Channel.CreateBounded<RuleUpdateContract>(new BoundedChannelOptions(capacity: 10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public void SendRule(RuleContract ruleContract)
    {
        RuleChannel.Writer.TryWrite(ruleContract);
    }

    public void SendRuleUpdate(RuleUpdateContract ruleUpdateContract)
    {
        RuleUpdateChannel.Writer.TryWrite(ruleUpdateContract);
    }

    public Channel<RuleContract> RuleChannel { get; }
    public Channel<RuleUpdateContract> RuleUpdateChannel { get; }
    public ChannelReader<RuleHub.RuleCreation> RuleCreationChannel => _ruleCreationChannel.Reader;
    public ChannelReader<RuleHub.RuleEnablement> RuleEnablementChannel => _ruleEnablementChannel.Reader;
    public ChannelReader<RuleHub.RuleDisablement> RuleDisablementChannel => _ruleDisablementChannel.Reader;
    public ChannelReader<RuleHub.RuleDeletion> RuleDeletionChannel => _ruleDeletionChannel.Reader;
    public ChannelReader<RuleHub.RuleCodeUpdate> RuleCodeUpdateChannel => _ruleCodeUpdateChannel.Reader;
    public ChannelReader<RuleHub.RuleAuditPolicyPreference> AuditPolicyPreferenceChannel => _auditPolicyPreferenceChannel.Reader;
    public IObserver<RuleHub.RuleCreation> RuleCreationObservable => _ruleCreationSubject;
    public IObserver<RuleHub.RuleEnablement> RuleEnablementObservable => _ruleEnablementSubject;
    public IObserver<RuleHub.RuleDisablement> RuleDisablementObservable => _ruleDisablementSubject;
    public IObserver<RuleHub.RuleDeletion> RuleDeletionObservable => _ruleDeletionSubject;
    public IObserver<RuleHub.RuleCodeUpdate> RuleCodeUpdateObservable => _ruleCodeUpdateSubject;
    public IObserver<RuleHub.RuleAuditPolicyPreference> AuditPolicyPreferenceObservable => _auditPolicyPreferenceSubject;

    private IDisposable SubscribeToRuleCreation()
    {
        return _ruleCreationSubject.Do(ruleCreation => _ruleCreationChannel.Writer.TryWrite(ruleCreation)).Subscribe();
    }

    private IDisposable SubscribeToRuleEnablement()
    {
        return _ruleEnablementSubject.Do(ruleEnablement => _ruleEnablementChannel.Writer.TryWrite(ruleEnablement)).Subscribe();
    }

    private IDisposable SubscribeToRuleDisablement()
    {
        return _ruleDisablementSubject.Do(ruleDisablement => _ruleDisablementChannel.Writer.TryWrite(ruleDisablement)).Subscribe();
    }

    private IDisposable SubscribeToRuleDeletion()
    {
        return _ruleDeletionSubject.Do(ruleDeletion => _ruleDeletionChannel.Writer.TryWrite(ruleDeletion)).Subscribe();
    }

    private IDisposable SubscribeToRuleCodeUpdate()
    {
        return _ruleCodeUpdateSubject.Do(ruleCodeUpdate => _ruleCodeUpdateChannel.Writer.TryWrite(ruleCodeUpdate)).Subscribe();
    }
    
    private IDisposable SubscribeToAuditPolicyPreference()
    {
        return _auditPolicyPreferenceSubject.Do(preference => _auditPolicyPreferenceChannel.Writer.TryWrite(preference)).Subscribe();
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _ruleCreationSubject.Dispose();
        _ruleEnablementSubject.Dispose();
        _ruleDisablementSubject.Dispose();
        _ruleDeletionSubject.Dispose();
        _ruleCodeUpdateSubject.Dispose();
        _auditPolicyPreferenceSubject.Dispose();
    }
}