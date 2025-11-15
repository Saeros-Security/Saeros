using System.Buffers;
using System.Runtime.CompilerServices;
using Collector.Core.Extensions;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Domain.Tracing.Buckets;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Abstractions.Stores.Tracing.Buckets;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Collector.Databases.Implementation.Stores.Tracing.Resolvers;
using MessagePack;
using Microsoft.Data.Sqlite;
using QuikGraph;
using Shared.Models.Console.Requests;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;

/// <summary>
/// Indexed by LogonId
/// </summary>
internal sealed class WorkstationBucket(ITracingRepository tracingRepository, IGeolocationService geolocationService, ILogonStore logonStore, Func<Type, IBucket> bucketProvider)
    : Bucket<long, Workstation, Workstation, Process>(tracingRepository, geolocationService, logonStore, nameof(WorkstationBucket), bucketProvider)
{
    protected override string SerializeKey(long key)
    {
        return LogonHelper.ToLogonId(key);
    }
    
    protected override string Hash(long key, Workstation value)
    {
        return $"{BucketName};{SerializeKey(key)};{value.Domain};{value.WorkstationName}".ToLowerInvariant();
    }

    protected override long DeserializeKey(string key)
    {
        return LogonHelper.FromLogonId(key);
    }

    protected override void SerializeValue(Workstation value, IBufferWriter<byte> writer)
    {
        MessagePackSerializer.Serialize(writer, value, TracingMessagePackResolver.Instance.Options);
    }

    protected override Workstation DeserializeValue(Stream serialized)
    {
        return MessagePackSerializer.Deserialize<Workstation>(serialized, TracingMessagePackResolver.Instance.Options);
    }

    protected override bool IsNull(Workstation value) => value.Date == DateTimeOffset.MinValue;

    protected override DateTimeOffset Date(Workstation value) => value.Date;

    protected override string WorkstationName(Workstation value)
    {
        return value.WorkstationName.ToLowerInvariant();
    }

    public async IAsyncEnumerable<EdgeRecord<long, Workstation, Process>> EnumerateEdgesAsync(SqliteConnection sqliteConnection, long key, Workstation workstation, TracingNode workstationNode, TracingQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var processBucket = GetProcessBucket();
        var processTreeBucket = GetProcessTreeBucket();
        await foreach (var process in processBucket.GetValuesAsync(sqliteConnection, query, key, cancellationToken))
        {
            var processKey = new ProcessKey(process.WorkstationName, process.Domain, process.ProcessId.ParseLong(), process.ProcessName, process.LogonId);
            var processTree = await processTreeBucket.GetAsync(sqliteConnection, processKey, cancellationToken);
            if (TryGetProcessVertex(process, processTree, query, out var target))
            {
                yield return new EdgeRecord<long, Workstation, Process>(key, workstation, process, workstationNode, target);
            }
        }
    }
}