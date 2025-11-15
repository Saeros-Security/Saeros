using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Databases.Abstractions.Domain.Tracing;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Implementation.Caching.Series;
using Collector.Databases.Implementation.Contexts.Tracing;
using Collector.Databases.Implementation.Extensions;
using Collector.Databases.Implementation.Helpers;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Models.Console.Requests;

namespace Collector.Databases.Implementation.Repositories.Tracing;

public sealed class TracingRepository : ITracingRepository
{
    private readonly ILogger<TracingRepository> _logger;
    private readonly TracingContext _context;
    private readonly DataFlowHelper.PeriodicBlock<TraceRecord> _insertBlock;
    private readonly IDisposable _subscription;
    private readonly TracingSeries _tracingSeries;
    private const int MaxHashCountByProcess = 25;
    
    public TracingRepository(ILogger<TracingRepository> logger, IHostApplicationLifetime applicationLifetime, TracingContext context, TracingSeries tracingSeries)
    {
        _logger = logger;
        _context = context;
        _tracingSeries = tracingSeries;
        _insertBlock = CreateBlock(applicationLifetime.ApplicationStopping, out var disposable);
        _subscription = disposable;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(_tracingSeries.InitializeAsync(cancellationToken), PopulateHashesAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }
    
    private async Task PopulateHashesAsync(CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Hash, Date FROM Hashes;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            _tracingSeries.Insert(reader.GetString(0), new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero));
        }
    }
    
    public async ValueTask InsertAsync(TraceRecord traceRecord, CancellationToken cancellationToken)
    {
        if (!await _insertBlock.SendAsync(traceRecord, cancellationToken))
        {
            _logger.Throttle(nameof(TracingRepository), itself => itself.LogError("Could not post trace"), expiration: TimeSpan.FromMinutes(1));
        }
        else
        {
            _tracingSeries.Insert(traceRecord.Hash, traceRecord.Date);
        }
    }

    public bool Contains(string hash) => _tracingSeries.Contains(hash);

    public async ValueTask<TValue> GetValueAsync<TValue>(SqliteConnection connection, string hash, Func<Stream, TValue> deserialize, CancellationToken cancellationToken) where TValue : struct
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
                SELECT Value
                FROM Hashes
                WHERE Hash = @Hash;
            """;

        command.Parameters.Add(new SqliteParameter("Hash", DatabaseHelper.GetValue(hash)));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            await using var stream = reader.GetStream(0);
            return deserialize(stream);
        }

        return default;
    }
    
    public async IAsyncEnumerable<KeyValuePair<string, TValue>> EnumerateKeyValuesAsync<TValue>(SqliteConnection connection, Func<Stream, TValue> deserialize, TracingQuery query, string bucket, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var sb = new StringBuilder();
        sb.Append("""
                  SELECT Max(Date), Key, Value
                  FROM Hashes
                  WHERE Bucket = @Bucket
                  """);

        var filteringKey = false;
        if (query.SearchTerms.TryGetValue(TracingSearchType.ProcessName, out var processName))
        {
            sb.Append(" AND Key IN (SELECT DISTINCT Key FROM Hashes WHERE ProcessName = @ProcessName)");
            command.Parameters.Add(new SqliteParameter("ProcessName", DatabaseHelper.GetValue(processName.ToLowerInvariant())));
            filteringKey = true;
        }
        
        if (query.SearchTerms.TryGetValue(TracingSearchType.WorkstationName, out var workstationName))
        {
            sb.Append(" AND Key IN (SELECT DISTINCT Key FROM Hashes WHERE WorkstationName = @WorkstationName)");
            command.Parameters.Add(new SqliteParameter("WorkstationName", DatabaseHelper.GetValue(workstationName.ToLowerInvariant())));
            filteringKey = true;
        }

        if (query.SearchTerms.TryGetValue(TracingSearchType.LogonId, out var logonId))
        {
            sb.Append(" AND Key IN (SELECT DISTINCT Key FROM Hashes WHERE LogonId = @LogonId)");
            command.Parameters.Add(new SqliteParameter("LogonId", DatabaseHelper.GetValue(LogonHelper.FromLogonId(logonId))));
            filteringKey = true;
        }
        else if (query.SearchTerms.ContainsKey(TracingSearchType.LogonTime))
        {
            sb.Append(" AND Key IN (SELECT DISTINCT Key FROM Hashes WHERE LogonId > 0)");
            filteringKey = true;
        }
        
        if (query.SearchTerms.TryGetValue(TracingSearchType.UserName, out var userName))
        {
            sb.Append(" AND Key IN (SELECT DISTINCT Key FROM Hashes WHERE UserName = @UserName)");
            command.Parameters.Add(new SqliteParameter("UserName", DatabaseHelper.GetValue(userName.ToLowerInvariant())));
            filteringKey = true;
        }
        
        if (query.SearchTerms.TryGetValue(TracingSearchType.UserSid, out var userSid))
        {
            sb.Append(" AND Key IN (SELECT DISTINCT Key FROM Hashes WHERE UserSid = @UserSid)");
            command.Parameters.Add(new SqliteParameter("UserSid", DatabaseHelper.GetValue(userSid.ToLowerInvariant())));
            filteringKey = true;
        }
        
        if (query.SearchTerms.TryGetValue(TracingSearchType.IpAddressUser, out var ipAddressUser))
        {
            sb.Append(" AND Key IN (SELECT DISTINCT Key FROM Hashes WHERE IpAddressUser = @IpAddressUser)");
            command.Parameters.Add(new SqliteParameter("IpAddressUser", DatabaseHelper.GetValue(ipAddressUser.ToLowerInvariant())));
            filteringKey = true;
        }
        
        if (!filteringKey)
        {
            sb.Append(" AND Key IN (SELECT DISTINCT Key FROM Hashes WHERE Bucket = @Bucket)");
        }
        
        if (query.SearchTerms.TryGetValue(TracingSearchType.LogonTime, out var logonTime) && DateTimeOffset.TryParseExact(logonTime, format: "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
        {
            sb.Append(" AND Date >= @MinLogonTime");
            sb.Append(" AND Date <= @MaxLogonTime");
            command.Parameters.Add(new SqliteParameter("MinLogonTime", DatabaseHelper.GetValue(time.TrimToMinutes().Subtract(TimeSpan.FromMinutes(1)).Ticks)));
            command.Parameters.Add(new SqliteParameter("MaxLogonTime", DatabaseHelper.GetValue(time.TrimToMinutes().Add(TimeSpan.FromMinutes(1)).Ticks)));
        }
        
        sb.Append(" GROUP BY Key;");
        command.Parameters.Add(new SqliteParameter("Bucket", bucket));
        command.CommandText = sb.ToString();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            await using var stream = reader.GetStream(2);
            yield return new KeyValuePair<string, TValue>(reader.GetString(1), deserialize(stream));
        }
    }

    public async IAsyncEnumerable<TValue> EnumerateValuesAsync<TValue>(SqliteConnection connection, Func<Stream, TValue> deserialize, TracingQuery query, string bucket, string key, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var sb = new StringBuilder();
        sb.Append("""
                  SELECT Value
                  FROM Hashes
                  WHERE Bucket = @Bucket AND Key = @Key
                  """);
        
        if (bucket.Equals(nameof(ProcessBucket)) && query.SearchTerms.TryGetValue(TracingSearchType.ProcessTime, out var processTime) && DateTimeOffset.TryParseExact(processTime, format: "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var processTimeValue))
        {
            sb.Append(" AND Date >= @MinProcessTime");
            sb.Append(" AND Date <= @MaxProcessTime");
            command.Parameters.Add(new SqliteParameter("MinProcessTime", DatabaseHelper.GetValue(processTimeValue.TrimToMinutes().Subtract(TimeSpan.FromMinutes(1)).Ticks)));
            command.Parameters.Add(new SqliteParameter("MaxProcessTime", DatabaseHelper.GetValue(processTimeValue.TrimToMinutes().Add(TimeSpan.FromMinutes(1)).Ticks)));
        }

        if (bucket.Equals(nameof(ProcessBucket)) && query.SearchTerms.TryGetValue(TracingSearchType.ProcessName, out var processName))
        {
            sb.Append(" AND ProcessName = @ProcessName");
            command.Parameters.Add(new SqliteParameter("ProcessName", DatabaseHelper.GetValue(processName.ToLowerInvariant())));
        }
        
        if (bucket.Equals(nameof(WorkstationBucket)) && query.SearchTerms.TryGetValue(TracingSearchType.WorkstationName, out var machineName))
        {
            sb.Append(" AND WorkstationName = @WorkstationName");
            command.Parameters.Add(new SqliteParameter("WorkstationName", DatabaseHelper.GetValue(machineName.ToLowerInvariant())));
        }
        
        sb.Append(" ORDER BY Date DESC;");
        command.Parameters.Add(new SqliteParameter("Bucket", DatabaseHelper.GetValue(bucket)));
        command.Parameters.Add(new SqliteParameter("Key", DatabaseHelper.GetValue(key)));
        command.CommandText = sb.ToString();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            await using var stream = reader.GetStream(0);
            yield return deserialize(stream);
        }
    }
    
    public async Task RemoveExceptAsync(IDictionary<string, ISet<string>> processNamesByWorkstationName, CancellationToken cancellationToken)
    {
        using var connection = await _context.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        
        var watch = Stopwatch.StartNew();
        var logonIdsByWorkstationName = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.Parameters.AddRange(processNamesByWorkstationName.Keys.Select((workstationName, index) => new SqliteParameter("@w" + index, workstationName)));
            command.CommandText = $"SELECT Key, WorkstationName FROM Hashes WHERE WorkstationName IN ({string.Join(",", processNamesByWorkstationName.Keys.Select((_, index) => "@w" + index))});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader.GetString(0);
                var workstationName = reader.GetString(1);
                if (logonIdsByWorkstationName.TryGetValue(workstationName, out var logonIds))
                {
                    logonIds.Add(key);
                }
                else
                {
                    logonIdsByWorkstationName.Add(workstationName, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key });
                }
            }
        }
        
        var logonIdsByProcessName = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in processNamesByWorkstationName)
        {
            if (logonIdsByWorkstationName.TryGetValue(kvp.Key, out var ids))
            {
                foreach (var processName in kvp.Value)
                {
                    if (logonIdsByProcessName.TryGetValue(processName, out var logonIds))
                    {
                        logonIds.AddRange(ids);
                    }
                    else
                    {
                        logonIdsByProcessName.Add(processName, new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase));
                    }
                }
            }
        }

        var userHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workstationHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.Parameters.AddRange(logonIdsByProcessName.Values.SelectMany(value => value).Select((logonId, index) => new SqliteParameter("@l" + index, logonId)));
            command.CommandText = $"SELECT Bucket, Hash FROM Hashes WHERE Bucket IN ('UserBucket', 'WorkstationBucket') AND Key IN ({string.Join(",", logonIdsByProcessName.Values.SelectMany(value => value).Select((_, index) => "@l" + index))});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var bucket = reader.GetString(0);
                var hash = reader.GetString(1);
                if (bucket.Equals(nameof(UserBucket), StringComparison.OrdinalIgnoreCase))
                {
                    userHashes.Add(hash);
                }
                else if (bucket.Equals(nameof(WorkstationBucket), StringComparison.OrdinalIgnoreCase))
                {
                    workstationHashes.Add(hash);
                }
            }
        }

        var processHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.CommandText = $"""
                                   WITH Temp AS (
                                   	SELECT
                                   		Hash,
                                   		ProcessName,
                                   		Key,
                                   		ROW_NUMBER() OVER (
                                   			PARTITION BY ProcessName
                                   			ORDER BY Date DESC
                                   		) AS row
                                   	FROM
                                   		Hashes
                                   	WHERE
                                   		ProcessName IS NOT NULL
                                   )
                                   SELECT Hash, ProcessName, Key FROM Temp WHERE row < {MaxHashCountByProcess};
                                   """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var hash = reader.GetString(0);
                var processName = reader.GetString(1);
                var key = reader.GetString(2);
                if (logonIdsByProcessName.TryGetValue(processName, out var logonIds))
                {
                    if (logonIds.Contains(key))
                    {
                        processHashes.Add(hash);
                    }
                }
            }
        }

        var removed = 0;
        await using (var transaction = connection.DbConnection.BeginTransaction())
        {
            await using (var command = connection.DbConnection.CreateCommand())
            {
                var hashParameter = command.Parameters.Add("Hash", SqliteType.Text);
                command.CommandText = "DELETE FROM Hashes WHERE Hash = @Hash;";
                foreach (var kvp in _tracingSeries.Enumerate())
                {
                    if (kvp.Value > DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(1))) continue;
                    if (userHashes.Contains(kvp.Key)) continue;
                    if (workstationHashes.Contains(kvp.Key)) continue;
                    if (processHashes.Contains(kvp.Key)) continue;
                    
                    hashParameter.Value = DatabaseHelper.GetValue(kvp.Key);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    _tracingSeries.Remove(kvp.Key);
                    removed++;
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (removed > 0)
        {
            _logger.LogInformation("Removed '{Count}' traces in '{Time}ms'", removed, watch.Elapsed.TotalMilliseconds);
            await VacuumAsync(connection.DbConnection, cancellationToken);
        }
    }

    private async Task VacuumAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            var watch = Stopwatch.StartNew();
            _logger.LogInformation("Performing vacuum...");
            command.CommandText = "VACUUM;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Vacuum completed in '{Time}ms'", watch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }

    private async Task InsertAsync(IList<TraceRecord> records, CancellationToken cancellationToken)
    {
        try
        {
            if (records.Count == 0) return;
            using var connection = await _context.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var transaction = connection.DbConnection.BeginTransaction();

            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO Hashes (Bucket, Key, Hash, Date, Value, LogonId, UserName, UserSid, IpAddressUser, WorkstationName, ProcessName) VALUES (@Bucket, @Key, @Hash, @Date, @Value, @LogonId, @UserName, @UserSid, @IpAddressUser, @WorkstationName, @ProcessName)";

            var bucketParameter = command.Parameters.Add("Bucket", SqliteType.Text);
            var keyParameter = command.Parameters.Add("Key", SqliteType.Text);
            var hashParameter = command.Parameters.Add("Hash", SqliteType.Text);
            var dateParameter = command.Parameters.Add("Date", SqliteType.Integer);
            var valueParameter = command.Parameters.Add("Value", SqliteType.Blob);
            var logonIdParameter = command.Parameters.Add("LogonId", SqliteType.Integer);
            var usernameParameter = command.Parameters.Add("UserName", SqliteType.Text);
            var userSidParameter = command.Parameters.Add("UserSid", SqliteType.Text);
            var ipAddressUserParameter = command.Parameters.Add("IpAddressUser", SqliteType.Text);
            var workstationNameParameter = command.Parameters.Add("WorkstationName", SqliteType.Text);
            var processNameParameter = command.Parameters.Add("ProcessName", SqliteType.Text);
            foreach (var record in records)
            {
                bucketParameter.Value = DatabaseHelper.GetValue(record.Bucket);
                keyParameter.Value = DatabaseHelper.GetValue(record.Key);
                hashParameter.Value = DatabaseHelper.GetValue(record.Hash);
                dateParameter.Value = DatabaseHelper.GetValue(record.Date.TrimToMinutes().Ticks);
                valueParameter.Value = DatabaseHelper.GetValue(record.Value);
                logonIdParameter.Value = DatabaseHelper.GetValue(record.LogonId);
                usernameParameter.Value = DatabaseHelper.GetValue(record.UserName);
                userSidParameter.Value = DatabaseHelper.GetValue(record.UserSid);
                ipAddressUserParameter.Value = DatabaseHelper.GetValue(record.IpAddressUser);
                workstationNameParameter.Value = DatabaseHelper.GetValue(record.WorkstationName);
                processNameParameter.Value = DatabaseHelper.GetValue(record.ProcessName);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error has occurred");
        }
    }
    
    private DataFlowHelper.PeriodicBlock<TraceRecord> CreateBlock(CancellationToken cancellationToken, out IDisposable disposableLink)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            SingleProducerConstrained = false,
            BoundedCapacity = 8,
            CancellationToken = cancellationToken
        };

        var periodicBlock = DataFlowHelper.CreatePeriodicBlock<TraceRecord>(TimeSpan.FromSeconds(1), count: 1000);
        var options = new DataflowLinkOptions { PropagateCompletion = true };
        var propagationBlock = new ActionBlock<IList<TraceRecord>>(async items => { await InsertAsync(items, cancellationToken); }, executionDataflow);
        disposableLink = periodicBlock.LinkTo(propagationBlock, options);
        return periodicBlock;
    }

    public async ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        await _insertBlock.DisposeAsync();
        await _tracingSeries.DisposeAsync();
    }
}