using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Collector.Core.Extensions;
using Collector.Databases.Implementation.Helpers;
using Collector.Detection.Events.Details;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Helpers;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Serialization;
using Exclusion = Collector.Databases.Abstractions.Domain.Exclusions.Exclusion;

namespace Collector.Databases.Implementation.Repositories.Detections;

public sealed partial class DetectionRepository
{
    private readonly struct ExclusionQueryKey(string ruleId, DateTimeOffset created) : IEquatable<ExclusionQueryKey>
    {
        private string RuleId { get; } = ruleId;
        private DateTimeOffset Created { get; } = created;

        public bool Equals(ExclusionQueryKey other)
        {
            return RuleId == other.RuleId && Created.Equals(other.Created);
        }

        public override bool Equals(object? obj)
        {
            return obj is ExclusionQueryKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RuleId, Created);
        }
    }

    private sealed class ExclusionQueryMetric(DateTimeOffset lastExcluded, long count)
    {
        public DateTimeOffset LastExcluded { get; set; } = lastExcluded;
        public long Count { get; set; } = count;
    }

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<DateTimeOffset, CreateRuleExclusion>> _exclusionsByRuleId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<ExclusionQueryKey, ExclusionQueryMetric> _exclusionMetricByKey = new();

    public async Task<IEnumerable<Shared.Models.Console.Responses.Exclusion>> GetExclusions(Func<string, Task<Rule?>> getRule, CancellationToken cancellationToken)
    {
        var exclusions = new List<Shared.Models.Console.Responses.Exclusion>();
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Value FROM Exclusions;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            var createRuleExclusion = JsonSerializer.Deserialize<CreateRuleExclusion>(Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(1)), SerializationContext.Default.CreateRuleExclusion);
            if (createRuleExclusion is null) continue;
            var rule = await getRule(createRuleExclusion.RuleId);
            if (rule is null) continue;
            if (_exclusionMetricByKey.TryGetValue(new ExclusionQueryKey(createRuleExclusion.RuleId, createRuleExclusion.Created), out var metric))
            {
                exclusions.Add(new Shared.Models.Console.Responses.Exclusion(id, createRuleExclusion.Created, metric.LastExcluded, metric.Count, rule, createRuleExclusion.Computers.ToHashSet(), createRuleExclusion.Attributes));
            }
            else
            {
                exclusions.Add(new Shared.Models.Console.Responses.Exclusion(id, createRuleExclusion.Created, activity: null, count: 0, rule, createRuleExclusion.Computers.ToHashSet(), createRuleExclusion.Attributes));
            }
        }

        return exclusions;
    }

    public async Task DeleteExclusion(int exclusionId, CancellationToken cancellationToken)
    {
        using var connection = await _detectionContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.CommandText = "SELECT Value FROM Exclusions WHERE Id = @Id;";
            command.Parameters.AddWithValue("Id", DatabaseHelper.GetValue(exclusionId));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var createRuleExclusion = JsonSerializer.Deserialize<CreateRuleExclusion>(Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)), SerializationContext.Default.CreateRuleExclusion);
                if (createRuleExclusion is null) return;
                var key = new ExclusionQueryKey(createRuleExclusion.RuleId, createRuleExclusion.Created);
                _exclusionMetricByKey.TryRemove(key, out _);
                if (_exclusionsByRuleId.TryGetValue(createRuleExclusion.RuleId, out var exclusions))
                {
                    exclusions.TryRemove(createRuleExclusion.Created, out _);
                }
            }
        }
        
        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Exclusions WHERE Id = @Id;";
            command.Parameters.AddWithValue("Id", DatabaseHelper.GetValue(exclusionId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task Exclude(CreateRuleExclusion createRuleExclusion, CancellationToken cancellationToken)
    {
        using var connection = await _detectionContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Exclusions (Value, Hash, Activity) VALUES (@Value, @Hash, @Activity);";
        var valueParameter = command.Parameters.Add("Value", SqliteType.Blob);
        valueParameter.Value = DatabaseHelper.GetValue(JsonSerializer.SerializeToUtf8Bytes(createRuleExclusion, SerializationContext.Default.CreateRuleExclusion));
        var hashParameter = command.Parameters.Add("Hash", SqliteType.Text);
        hashParameter.Value = DatabaseHelper.GetValue(Hash(createRuleExclusion));
        var activityParameter = command.Parameters.Add("Activity", SqliteType.Integer);
        activityParameter.Value = DatabaseHelper.GetValue(DateTimeOffset.UtcNow.Ticks);
        var row = await command.ExecuteNonQueryAsync(cancellationToken);
        if (row == 0) return;
        _exclusionsByRuleId.AddOrUpdate(createRuleExclusion.RuleId, addValueFactory: _ =>
        {
            var dictionary = new ConcurrentDictionary<DateTimeOffset, CreateRuleExclusion>();
            dictionary.TryAdd(createRuleExclusion.Created, createRuleExclusion);
            return dictionary;
        }, updateValueFactory: (_, current) =>
        {
            current.TryAdd(createRuleExclusion.Created, createRuleExclusion);
            return current;
        });

        _exclusionBlock.Post(createRuleExclusion);
    }

    private static string Hash(CreateRuleExclusion createRuleExclusion)
    {
        return Sha1Helper.Hash(JsonSerializer.Serialize(new Exclusion(createRuleExclusion.RuleId, createRuleExclusion.Computers, createRuleExclusion.Attributes)));
    }

    private async Task ProcessExclusionAsync(CreateRuleExclusion createRuleExclusion, CancellationToken cancellationToken)
    {
        try
        {
            var key = new ExclusionQueryKey(createRuleExclusion.RuleId, createRuleExclusion.Created);
            var ruleDetails = await GetRuleDetailsAsync(createRuleExclusion.RuleId, cancellationToken);
            var excludedDetectionIds = new HashSet<long>();

            await using (var readConnection = _detectionContext.CreateConnection())
            {
                await readConnection.OpenAsync(cancellationToken);
                var computerNames = createRuleExclusion.Computers.Select(computer => computer.Name).ToArray();
                await using (var command = readConnection.CreateCommand())
                {
                    command.Parameters.AddRange(computerNames.Select((p, index) => new SqliteParameter("@p" + index, p)));
                    command.CommandText = $@"SELECT D.Id, D.Date, C.Value, S.Value, P.Value, P.Guid, H.Value, D.EventId
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Details AS S ON S.Id = D.DetailsId
INNER JOIN Providers AS P ON P.Id = D.ProviderId
INNER JOIN Channels AS H ON H.Id = D.ChannelId
WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE Value IN ({string.Join(",", computerNames.Select((_, index) => "@p" + index))})) AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1) AND D.RuleId IN (SELECT Id FROM Rules WHERE Value = @RuleId);";
                    var ruleIdParameter = command.Parameters.Add("RuleId", SqliteType.Text);
                    ruleIdParameter.Value = DatabaseHelper.GetValue(createRuleExclusion.RuleId);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var (winEvent, detectionId) = GetWinEventCore(reader, ruleDetails);
                        if (IsExcluded(winEvent, createRuleExclusion))
                        {
                            excludedDetectionIds.Add(detectionId);
                        }
                    }
                }
            }

            using (var writeConnection = await _detectionContext.CreateConnectionAsync(cancellationToken))
            {
                await writeConnection.DbConnection.OpenAsync(cancellationToken);
                await using (var transaction = writeConnection.DbConnection.BeginTransaction())
                {
                    await using (var command = writeConnection.DbConnection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM Detections WHERE Id = @Id;";
                        var idParameter = command.Parameters.Add("Id", SqliteType.Integer);
                        foreach (var detectionId in excludedDetectionIds)
                        {
                            idParameter.Value = DatabaseHelper.GetValue(detectionId);
                            await command.ExecuteNonQueryAsync(cancellationToken);
                            _exclusionMetricByKey.AddOrUpdate(key, addValueFactory: _ => new ExclusionQueryMetric(DateTimeOffset.UtcNow, count: 1), updateValueFactory: (_, current) =>
                            {
                                current.LastExcluded = DateTimeOffset.UtcNow;
                                current.Count++;
                                return current;
                            });
                        }
                    }

                    await transaction.CommitAsync(cancellationToken);
                }

                await PopulateStoreAsync(writeConnection.DbConnection, cancellationToken, createRuleExclusion.RuleId);
            }

            await ComputeMetricsAsync(cancellationToken);
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

    private async Task LoadExclusionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value, Count, Activity FROM Exclusions;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var createRuleExclusion = JsonSerializer.Deserialize<CreateRuleExclusion>(Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0)), SerializationContext.Default.CreateRuleExclusion);
                if (createRuleExclusion is null) continue;
                var count = reader.GetInt64(1);
                var activity = reader.GetInt64(2);
                var key = new ExclusionQueryKey(createRuleExclusion.RuleId, createRuleExclusion.Created);
                _exclusionsByRuleId.AddOrUpdate(createRuleExclusion.RuleId, addValueFactory: _ =>
                {
                    var dictionary = new ConcurrentDictionary<DateTimeOffset, CreateRuleExclusion>();
                    dictionary.TryAdd(createRuleExclusion.Created, createRuleExclusion);
                    return dictionary;
                }, updateValueFactory: (_, current) =>
                {
                    current.TryAdd(createRuleExclusion.Created, createRuleExclusion);
                    return current;
                });
                
                _exclusionMetricByKey.AddOrUpdate(key, addValueFactory: _ => new ExclusionQueryMetric(new DateTimeOffset(activity, TimeSpan.Zero), count), updateValueFactory: (_, current) =>
                {
                    current.LastExcluded = new DateTimeOffset(activity, TimeSpan.Zero);
                    current.Count = count;
                    return current;
                });
            }
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

    private async Task SaveExclusionMetrics(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await _detectionContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            var ids = new Dictionary<int, ExclusionQueryKey>();
            await using (var command = connection.DbConnection.CreateCommand())
            {
                command.CommandText = "SELECT Id, Value FROM Exclusions;";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var id = reader.GetInt32(0);
                    var createRuleExclusion = JsonSerializer.Deserialize<CreateRuleExclusion>(Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(1)), SerializationContext.Default.CreateRuleExclusion);
                    if (createRuleExclusion is null) continue;
                    ids.Add(id, new ExclusionQueryKey(createRuleExclusion.RuleId, createRuleExclusion.Created));
                }
            }

            await using (var transaction = connection.DbConnection.BeginTransaction())
            {
                await using var command = connection.DbConnection.CreateCommand();
                command.CommandText = "UPDATE Exclusions SET Count = @Count, Activity = @Activity WHERE Id = @Id;";
                var idParameter = command.Parameters.Add("Id", SqliteType.Integer);
                var countParameter = command.Parameters.Add("Count", SqliteType.Integer);
                var activityParameter = command.Parameters.Add("Activity", SqliteType.Integer);
                foreach (var kvp in ids)
                {
                    if (_exclusionMetricByKey.TryGetValue(kvp.Value, out var metric))
                    {
                        idParameter.Value = DatabaseHelper.GetValue(kvp.Key);
                        countParameter.Value = DatabaseHelper.GetValue(metric.Count);
                        activityParameter.Value = DatabaseHelper.GetValue(metric.LastExcluded.Ticks);
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                await transaction.CommitAsync(cancellationToken);
            }
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
    
    private static (WinEvent WinEvent, long DetectionId) GetWinEventCore(SqliteDataReader reader, string? ruleDetails)
    {
        var detectionId = reader.GetInt64(0);
        var date = new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero);
        var computer = reader.GetString(2);
        var details = reader.GetFieldValue<byte[]>(3).LZ4UncompressString();
        var providerName = reader.GetString(4);
        var providerGuid = reader.GetString(5);
        var channelName = reader.GetString(6);
        var eventId = reader.GetInt32(7);
        return (DetectionDetailsResolver.Resolve(providerName, providerGuid, channelName, systemTime: date.ToString("O"), computer, eventId.ToString(), ruleDetails, details), detectionId);
    }

    private static bool IsExcluded(WinEvent winEvent, string computerName, CreateRuleExclusion createRuleExclusion)
    {
        if (!createRuleExclusion.Computers.Select(computer => computer.Name).Contains(computerName, StringComparer.OrdinalIgnoreCase)) return false;
        return IsExcluded(winEvent, createRuleExclusion);
    }

    private static bool IsExcluded(WinEvent winEvent, CreateRuleExclusion createRuleExclusion)
    {
        foreach (var attribute in createRuleExclusion.Attributes)
        {
            if (!winEvent.EventData.TryGetValue(attribute.Key, out var value) || !value.Equals(attribute.Value, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }
}