using Collector.Databases.Abstractions.Domain.Tracing;
using Microsoft.Data.Sqlite;
using Shared.Models.Console.Requests;

namespace Collector.Databases.Abstractions.Repositories.Tracing;

public interface ITracingRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    ValueTask InsertAsync(TraceRecord traceRecord, CancellationToken cancellationToken);
    bool Contains(string hash);
    ValueTask<TValue> GetValueAsync<TValue>(SqliteConnection connection, string hash, Func<Stream, TValue> deserialize, CancellationToken cancellationToken) where TValue : struct;
    IAsyncEnumerable<TValue> EnumerateValuesAsync<TValue>(SqliteConnection connection, Func<Stream, TValue> deserialize, TracingQuery query, string bucket, string key, CancellationToken cancellationToken);
    IAsyncEnumerable<KeyValuePair<string, TValue>> EnumerateKeyValuesAsync<TValue>(SqliteConnection connection, Func<Stream, TValue> deserialize, TracingQuery query, string bucket, CancellationToken cancellationToken);
    Task RemoveExceptAsync(IDictionary<string, ISet<string>> processNamesByWorkstationName, CancellationToken cancellationToken);
}