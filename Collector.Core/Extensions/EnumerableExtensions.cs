using System.Threading.Tasks.Dataflow;

namespace Collector.Core.Extensions;

public static class EnumerableExtensions
{
    public static Task ProcessAllAsync<T>(this IEnumerable<T> items, Action<T> process, CancellationToken cancellationToken)
    {
        var action = new ActionBlock<T>(process, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            BoundedCapacity = DataflowBlockOptions.Unbounded,
            CancellationToken = cancellationToken
        });
        
        foreach (var item in items)
        {
            action.Post(item);
        }
        
        action.Complete();
        return action.Completion;
    }
}