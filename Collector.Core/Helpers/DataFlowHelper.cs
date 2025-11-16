using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;

namespace Collector.Core.Helpers;

public static class DataFlowHelper
{
    public abstract class PeriodicBlock<T> : IAsyncDisposable
    {
        public abstract ValueTask DisposeAsync();
        public abstract bool Post(T item);
        public abstract Task<bool> SendAsync(T item, CancellationToken cancellationToken);
        public abstract IDisposable LinkTo(ITargetBlock<T[]> target, DataflowLinkOptions linkOptions);
    }

    private sealed class BatchPeriodicBlock<T> : PeriodicBlock<T>, IPropagatorBlock<T, T[]>, IReceivableSourceBlock<T[]>
    {
        private readonly BatchBlock<T> _source;
        private readonly Timer _timer;

        public BatchPeriodicBlock(TimeSpan timeSpan, int count)
        {
            _source = new BatchBlock<T>(count, new GroupingDataflowBlockOptions
            {
                BoundedCapacity = count * 4,
                Greedy = true,
                EnsureOrdered = true
            });

            _timer = new Timer(_ => _source.TriggerBatch(), state: null, TimeSpan.Zero, timeSpan);
        }

        public BatchPeriodicBlock(TimeSpan timeSpan)
        {
            const int maxCapacity = 4096;
            _source = new BatchBlock<T>(batchSize: maxCapacity, new GroupingDataflowBlockOptions
            {
                BoundedCapacity = maxCapacity * 4,
                Greedy = true,
                EnsureOrdered = true
            });

            _timer = new Timer(_ => _source.TriggerBatch(), state: null, TimeSpan.Zero, timeSpan);
        }

        public Task Completion => _source.Completion;
        public void Complete() => _source.Complete();
        void IDataflowBlock.Fault(Exception exception) => ((IDataflowBlock)_source).Fault(exception);
        public override bool Post(T item) => _source.Post(item);
        public override Task<bool> SendAsync(T item, CancellationToken cancellationToken) => _source.SendAsync(item, cancellationToken);
        public override IDisposable LinkTo(ITargetBlock<T[]> target, DataflowLinkOptions linkOptions) => _source.LinkTo(target, linkOptions);
        public bool TryReceive(Predicate<T[]>? filter, [NotNullWhen(true)] out T[]? item) => _source.TryReceive(filter, out item);
        public bool TryReceiveAll([NotNullWhen(true)] out IList<T[]>? items) => _source.TryReceiveAll(out items);
        DataflowMessageStatus ITargetBlock<T>.OfferMessage(DataflowMessageHeader messageHeader, T messageValue, ISourceBlock<T>? source, bool consumeToAccept) => ((ITargetBlock<T>)_source).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        T[]? ISourceBlock<T[]>.ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<T[]> target, out bool messageConsumed) => ((ISourceBlock<T[]>)_source).ConsumeMessage(messageHeader, target, out messageConsumed);
        bool ISourceBlock<T[]>.ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<T[]> target) => ((ISourceBlock<T[]>)_source).ReserveMessage(messageHeader, target);
        void ISourceBlock<T[]>.ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<T[]> target) => ((ISourceBlock<T[]>)_source).ReleaseReservation(messageHeader, target);

        public override async ValueTask DisposeAsync()
        {
            Complete();
            await _timer.DisposeAsync();
        }
    }

    public static PeriodicBlock<TIn> CreatePeriodicBlock<TIn>(TimeSpan timeSpan, int count)
    {
        return new BatchPeriodicBlock<TIn>(timeSpan, count);
    }

    public static PeriodicBlock<TIn> CreatePeriodicBlock<TIn>(TimeSpan timeSpan)
    {
        return new BatchPeriodicBlock<TIn>(timeSpan);
    }
    
    public static IPropagatorBlock<TIn, IList<TIn>> CreateUnboundedPeriodicBlock<TIn>(TimeSpan timeSpan, int count)
    {
        var inBlock = new BufferBlock<TIn>();
        var outBlock = new BufferBlock<IList<TIn>>();

        var outObserver = outBlock.AsObserver();
        inBlock.AsObservable()
            .Buffer(timeSpan, count)
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(outObserver);

        return DataflowBlock.Encapsulate(inBlock, outBlock);
    }
}