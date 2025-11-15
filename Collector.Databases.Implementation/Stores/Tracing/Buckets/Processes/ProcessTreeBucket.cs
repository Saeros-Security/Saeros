using System.Buffers;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Abstractions.Stores.Tracing.Buckets;
using Collector.Databases.Implementation.Stores.Tracing.Resolvers;
using MessagePack;
using Microsoft.Data.Sqlite;
using Streaming;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;

internal sealed class ProcessTreeBucket(ITracingRepository tracingRepository, IGeolocationService geolocationService, ILogonStore logonStore, Func<Type, IBucket> bucketProvider)
    : Bucket<ProcessKey, ProcessTree, ProcessKey, ProcessTree>(tracingRepository, geolocationService, logonStore, nameof(ProcessTreeBucket), bucketProvider)
{
    protected override string SerializeKey(ProcessKey key)
    {
        return LogonHelper.ToLogonId(key.LogonId);
    }
    
    protected override string Hash(ProcessKey key, ProcessTree value)
    {
        return $"{BucketName};{SerializeKey(key)};{key.WorkstationName};{key.Domain};{key.ProcessId};{key.ProcessName}".ToLowerInvariant();
    }

    protected override ProcessKey DeserializeKey(string key)
    {
        var parts = key.Split(';', StringSplitOptions.RemoveEmptyEntries);
        return new ProcessKey(workstationName: parts[2], domain: parts[3], processId: long.Parse(parts[4]), processName: parts[5], logonId: LogonHelper.FromLogonId(parts[1]));
    }

    protected override void SerializeValue(ProcessTree value, IBufferWriter<byte> writer)
    {
        MessagePackSerializer.Serialize(writer, value, TracingMessagePackResolver.Instance.Options);
    }
    
    protected override ProcessTree DeserializeValue(Stream serialized)
    {
        return MessagePackSerializer.Deserialize<ProcessTree>(serialized, TracingMessagePackResolver.Instance.Options);
    }

    protected override bool IsNull(ProcessTree value) => string.IsNullOrEmpty(value.Value);

    protected override string ProcessName(ProcessTree value)
    {
        return Path.GetFileName(value.ProcessName).ToLowerInvariant();
    }
    
    public ValueTask AddAsync(ProcessTreeContract contract, CancellationToken cancellationToken) => AddAsync(new ProcessKey(contract.WorkstationName, contract.Domain, contract.ProcessId, contract.ProcessName, contract.LogonId), new ProcessTree(contract.ProcessTree, contract.ProcessName), cancellationToken);

    public async ValueTask<ProcessTree> GetAsync(SqliteConnection connection, ProcessKey processKey, CancellationToken cancellationToken)
    {
        var value = await GetValueAsync(connection, processKey, default, cancellationToken);
        if (IsNull(value)) return new ProcessTree();
        return value;
    }
}