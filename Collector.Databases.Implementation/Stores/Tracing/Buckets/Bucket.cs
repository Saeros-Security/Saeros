using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Domain.Tracing;
using Collector.Databases.Abstractions.Domain.Tracing.Buckets;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Abstractions.Stores.Tracing.Buckets;
using Collector.Databases.Implementation.Extensions;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;
using Collector.Databases.Implementation.Stores.Tracing.Extensions;
using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.Data.Sqlite;
using QuikGraph;
using Shared.Models.Console.Requests;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets;

internal abstract class Bucket<TKey, TValue, TSource, TTarget>(ITracingRepository tracingRepository, IGeolocationService geolocationService, ILogonStore logonStore, string bucketName, Func<Type, IBucket> bucketProvider)
    : IBucket<TKey, TValue, TSource, TTarget>
    where TKey : notnull
    where TValue : struct
{
    private T GetBucket<T>() where T : IBucket
    {
        return (T)bucketProvider(typeof(T));
    }

    protected abstract string Hash(TKey key, TValue value);
    protected abstract string SerializeKey(TKey key);
    protected abstract TKey DeserializeKey(string key);
    protected abstract void SerializeValue(TValue value, IBufferWriter<byte> writer);
    protected abstract TValue DeserializeValue(Stream serialized);
    protected abstract bool IsNull(TValue value);
    protected virtual DateTimeOffset Date(TValue value) => DateTimeOffset.UtcNow;
    protected virtual long? LogonId(TValue value) => null;
    protected virtual string? UserName(TValue value) => null;
    protected virtual string? UserSid(TValue value) => null;
    protected virtual string? IpAddressUser(TValue value) => null;
    protected virtual string? WorkstationName(TValue value) => null;
    protected virtual string? ProcessName(TValue value) => null;

    protected bool TryGetUserVertex(User user, TracingQuery query, [MaybeNullWhen(false)] out TracingNode userNode)
    {
        userNode = null;
        if (query.SearchTerms.TryGetValue(TracingSearchType.LogonTime, out var logonTime) && DateTimeOffset.TryParseExact(logonTime, format: "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
        {
            if (!(user.Date.TrimToMinutes() >= time.TrimToMinutes().Subtract(TimeSpan.FromMinutes(1)) && user.Date.TrimToMinutes() <= time.TrimToMinutes().AddMinutes(1))) return false;
        }
        
        if (query.SearchTerms.TryGetValue(TracingSearchType.UserName, out var userName))
        {
            if (!user.Name.Equals(userName, StringComparison.OrdinalIgnoreCase)) return false;
        }

        if (query.SearchTerms.TryGetValue(TracingSearchType.UserSid, out var userSid))
        {
            if (!user.Sid.Equals(userSid, StringComparison.OrdinalIgnoreCase)) return false;
        }

        if (query.SearchTerms.TryGetValue(TracingSearchType.LogonId, out var logonId))
        {
            if (!user.LogonId.Equals(LogonHelper.FromLogonId(logonId))) return false;
        }
        
        if (query.SearchTerms.TryGetValue(TracingSearchType.IpAddressUser, out var ipAddressUser))
        {
            if (!user.SourceIp.Equals(ipAddressUser, StringComparison.OrdinalIgnoreCase)) return false;
        }

        userNode = user.ToTracingNode(geolocationService);
        return true;
    }

    protected bool TryGetWorkstationVertex(Workstation workstation, TracingQuery query, [MaybeNullWhen(false)] out TracingNode workstationNode)
    {
        workstationNode = null;
        if (query.SearchTerms.TryGetValue(TracingSearchType.WorkstationName, out var machineName))
        {
            if (!workstation.WorkstationName.Equals(machineName, StringComparison.OrdinalIgnoreCase)) return false;
        }

        workstationNode = workstation.ToTracingNode(logonStore);
        return true;
    }

    protected static bool TryGetProcessVertex(Process process, ProcessTree processTree, TracingQuery query, [MaybeNullWhen(false)] out TracingNode processNode)
    {
        processNode = null;
        if (query.SearchTerms.TryGetValue(TracingSearchType.ProcessTime, out var processTime) && DateTimeOffset.TryParseExact(processTime, format: "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
        {
            if (!(process.Date.TrimToMinutes() >= time.TrimToMinutes().Subtract(TimeSpan.FromMinutes(1)) && process.Date.TrimToMinutes() <= time.TrimToMinutes().AddMinutes(1))) return false;
        }
        
        if (query.SearchTerms.TryGetValue(TracingSearchType.ProcessName, out var processName))
        {
            if (!Path.GetFileName(process.ProcessName).Equals(processName, StringComparison.OrdinalIgnoreCase)) return false;
        }

        processNode = process.ToTracingNode(processTree);
        return true;
    }
    
    protected WorkstationBucket GetWorkstationBucket() => GetBucket<WorkstationBucket>();
    protected ProcessBucket GetProcessBucket() => GetBucket<ProcessBucket>();
    protected ProcessTreeBucket GetProcessTreeBucket() => GetBucket<ProcessTreeBucket>();

    protected async IAsyncEnumerable<KeyValuePair<TKey, TValue>> EnumerateAsync(SqliteConnection connection, TracingQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var kvp in tracingRepository.EnumerateKeyValuesAsync(connection, DeserializeValue, query, BucketName, cancellationToken))
        {
            if (IsNull(kvp.Value)) continue;
            yield return new KeyValuePair<TKey, TValue>(DeserializeKey(kvp.Key), kvp.Value);
        }
    }

    public string BucketName { get; } = bucketName;
    
    public bool Contains(TKey key, TValue value) => tracingRepository.Contains(Hash(key, value));

    public ValueTask<TValue> GetValueAsync(SqliteConnection connection, TKey key, TValue value, CancellationToken cancellationToken)
    {
        return tracingRepository.GetValueAsync(connection, Hash(key, value), DeserializeValue, cancellationToken);
    }

    public virtual async IAsyncEnumerable<EdgeRecord<TKey, TSource, TTarget>> EnumerateEdgesAsync(SqliteConnection sqliteConnection, TracingQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield break;
    }

    public async IAsyncEnumerable<TValue> GetValuesAsync(SqliteConnection connection, TracingQuery query, TKey key, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var value in tracingRepository.EnumerateValuesAsync(connection, DeserializeValue, query, BucketName, SerializeKey(key), cancellationToken))
        {
            if (IsNull(value)) continue;
            yield return value;
        }
    }

    public ValueTask AddAsync(TKey key, TValue value, CancellationToken cancellationToken)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializeValue(value, writer);
        return tracingRepository.InsertAsync(new TraceRecord(BucketName, SerializeKey(key), Hash(key, value), Date(value), Value: writer.WrittenSpan.ToArray(), LogonId(value), UserName(value), UserSid(value), IpAddressUser(value), WorkstationName(value), ProcessName(value)), cancellationToken);
    }
}