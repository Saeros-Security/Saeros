using Collector.Databases.Abstractions.Stores.Logon;
using QuikGraph;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Streaming;

namespace Collector.Databases.Abstractions.Stores.Tracing;

public interface ITracingStore : IAsyncDisposable
{
    Task ExecuteAsync(CancellationToken cancellationToken);
    ValueTask ProcessAsync(TraceContract contract, CancellationToken cancellationToken);
    ValueTask ProcessAsync(EventContract contract, CancellationToken cancellationToken);
    ValueTask ProcessAsync(ProcessTreeContract processTreeContract, CancellationToken cancellationToken);
    Task<IEdgeListGraph<TracingNode, IEdge<TracingNode>>> TrySerializeGraph(TracingQuery tracingQuery, CancellationToken cancellationToken);
    SortedDictionary<int, long> GetEventCountById();
    (IEnumerable<OutboundEntry> Entries, IDictionary<string, long> OutboundByCountry) GetOutboundValues();
    IEnumerable<AccountLogon> EnumerateSuccessLogons();
    IEnumerable<AccountLogon> EnumerateFailureLogons();
}