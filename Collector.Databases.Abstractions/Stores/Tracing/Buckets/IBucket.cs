using Collector.Databases.Abstractions.Domain.Tracing.Buckets;
using Microsoft.Data.Sqlite;
using Shared.Models.Console.Requests;

namespace Collector.Databases.Abstractions.Stores.Tracing.Buckets;

public interface IBucket<TKey, TValue, TSource, TTarget> : IBucket where TKey : notnull where TValue : struct
{
    bool Contains(TKey key, TValue value);
    ValueTask AddAsync(TKey key, TValue value, CancellationToken cancellationToken);
    ValueTask<TValue> GetValueAsync(SqliteConnection connection, TKey key, TValue value, CancellationToken cancellationToken);
    IAsyncEnumerable<EdgeRecord<TKey, TSource, TTarget>> EnumerateEdgesAsync(SqliteConnection connection, TracingQuery query, CancellationToken cancellationToken);
    IAsyncEnumerable<TValue> GetValuesAsync(SqliteConnection connection, TracingQuery query, TKey key, CancellationToken cancellationToken);
}

public interface IBucket
{
    string BucketName { get; }
}