using System.Buffers;
using System.Runtime.CompilerServices;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Domain.Tracing.Buckets;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Abstractions.Stores.Tracing.Buckets;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;
using Collector.Databases.Implementation.Stores.Tracing.Resolvers;
using MessagePack;
using Microsoft.Data.Sqlite;
using Shared.Models.Console.Requests;

namespace Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;

/// <summary>
/// Indexed by LogonId
/// </summary>
internal sealed class UserBucket(ITracingRepository tracingRepository, IGeolocationService geolocationService, ILogonStore logonStore, Func<Type, IBucket> bucketProvider)
    : Bucket<long, User, User, Workstation>(tracingRepository, geolocationService, logonStore, nameof(UserBucket), bucketProvider)
{
    protected override string SerializeKey(long key)
    {
        return LogonHelper.ToLogonId(key);
    }
    
    protected override string Hash(long key, User value)
    {
        return $"{BucketName};{SerializeKey(key)};{value.Domain};{value.Sid};{value.LogonId}".ToLowerInvariant();
    }

    protected override long DeserializeKey(string key)
    {
        return LogonHelper.FromLogonId(key);
    }

    protected override void SerializeValue(User value, IBufferWriter<byte> writer)
    {
        MessagePackSerializer.Serialize(writer, value, TracingMessagePackResolver.Instance.Options);
    }

    protected override User DeserializeValue(Stream serialized)
    {
        return MessagePackSerializer.Deserialize<User>(serialized, TracingMessagePackResolver.Instance.Options);
    }

    protected override bool IsNull(User value) => value.Date == DateTimeOffset.MinValue;
    
    protected override DateTimeOffset Date(User value) => value.Date;
    
    protected override long? LogonId(User value)
    {
        return value.LogonId;
    }

    protected override string UserName(User value)
    {
        return value.Name.ToLowerInvariant();
    }

    protected override string UserSid(User value)
    {
        return value.Sid.ToLowerInvariant();
    }

    protected override string IpAddressUser(User value)
    {
        return value.SourceIp.ToLowerInvariant();
    }

    public override async IAsyncEnumerable<EdgeRecord<long, User, Workstation>> EnumerateEdgesAsync(SqliteConnection sqliteConnection, TracingQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var workstationBucket = GetWorkstationBucket();
        await foreach (var kvp in EnumerateAsync(sqliteConnection, query, cancellationToken))
        {
            await foreach (var workstation in workstationBucket.GetValuesAsync(sqliteConnection, query, kvp.Key, cancellationToken))
            {
                if (TryGetUserVertex(kvp.Value, query, out var source) && TryGetWorkstationVertex(workstation, query, out var target))
                {
                    yield return new EdgeRecord<long, User, Workstation>(kvp.Key, kvp.Value, workstation, source, target);
                }
            }
        }
    }
}