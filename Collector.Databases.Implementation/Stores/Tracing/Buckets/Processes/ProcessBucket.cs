using System.Buffers;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Abstractions.Stores.Tracing.Buckets;
using Collector.Databases.Implementation.Stores.Tracing.Resolvers;
using MessagePack;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;

/// <summary>
/// Indexed by LogonId
/// </summary>
internal sealed class ProcessBucket(ITracingRepository tracingRepository, IGeolocationService geolocationService, ILogonStore logonStore, Func<Type, IBucket> bucketProvider)
    : Bucket<long, Process, Process, Process>(tracingRepository, geolocationService, logonStore, nameof(ProcessBucket), bucketProvider)
{
    protected override string SerializeKey(long key)
    {
        return LogonHelper.ToLogonId(key);
    }

    protected override string Hash(long key, Process value)
    {
        return $"{BucketName};{SerializeKey(key)};{value.WorkstationName};{value.Domain};{value.ProcessId};{value.ProcessName}".ToLowerInvariant();
    }
    
    protected override long DeserializeKey(string key)
    {
        return LogonHelper.FromLogonId(key);
    }

    protected override void SerializeValue(Process value, IBufferWriter<byte> writer)
    {
        MessagePackSerializer.Serialize(writer, value, TracingMessagePackResolver.Instance.Options);
    }

    protected override Process DeserializeValue(Stream serialized)
    {
        return MessagePackSerializer.Deserialize<Process>(serialized, TracingMessagePackResolver.Instance.Options);
    }

    protected override bool IsNull(Process value) => value.Date == DateTimeOffset.MinValue;
    
    protected override DateTimeOffset Date(Process value) => value.Date;

    protected override string ProcessName(Process value)
    {
        return Path.GetFileName(value.ProcessName).ToLowerInvariant();
    }
}