using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using App.Metrics;
using Collector.Core;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Core.Hubs.Detections;
using Collector.Core.Services;
using Collector.Core.SystemAudits;
using Collector.Databases.Abstractions.Caching.LRU;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Stores.Detections;
using Collector.Databases.Abstractions.Stores.Rules;
using Collector.Databases.Implementation.Contexts.Detections;
using Collector.Databases.Implementation.Contexts.Rules;
using Collector.Databases.Implementation.Helpers;
using Collector.Detection.Events.Details;
using Collector.Detection.Mitre;
using Google.Protobuf;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Extensions;
using Shared.Helpers;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Models.Detections;
using Shared.Serialization;
using Streaming;

namespace Collector.Databases.Implementation.Repositories.Detections;

public sealed partial class DetectionRepository : IDetectionRepository
{
    private readonly ILogger<DetectionRepository> _logger;
    private readonly DetectionContext _detectionContext;
    private readonly RuleContext _ruleContext;
    private readonly ISettingsRepository _settingsRepository;
    private readonly DataFlowHelper.PeriodicBlock<DetectionContract> _detectionBlock;
    private readonly ActionBlock<CreateRuleExclusion> _exclusionBlock;
    private readonly IStreamingDetectionHub _streamingDetectionHub;
    private readonly ISystemAuditService _systemAuditService;
    private readonly IDetectionStore _detectionStore;
    private readonly IRuleStore _ruleStore;
    private readonly IIntegrationService _integrationService;
    private readonly IMetricsRoot _metrics;
    private readonly IDisposable _subscription;

    private const string Insertion = @"INSERT OR IGNORE INTO Titles (Value) VALUES (@Title);
INSERT OR IGNORE INTO Rules (Value, Title) VALUES (@RuleGuid, @RuleTitle);
INSERT OR IGNORE INTO Levels (Value) VALUES (@LevelName);
INSERT OR IGNORE INTO Computers (Value) VALUES (@ComputerName);
INSERT OR IGNORE INTO Details (Hash, Value) VALUES (@DetailsHash, @DetailsValue);
INSERT OR IGNORE INTO Providers (Value, Guid) VALUES (@ProviderName, @ProviderGuid);
INSERT OR IGNORE INTO Channels (Value) VALUES (@ChannelName);
INSERT OR IGNORE INTO Mitres (MitreId, Tactic, Technique, SubTechnique) VALUES (@MitreId, @Tactic, @Technique, @SubTechnique);
INSERT INTO Detections (Id, TitleId, Duration, Date, RuleId, LevelId, ComputerId, DetailsId, ProviderId, ChannelId, EventId, MitreId) VALUES (@Id, (SELECT Id FROM Titles WHERE Value = @Title), @Duration, @Date, (SELECT Id FROM Rules WHERE Value = @RuleGuid),(SELECT Id FROM Levels WHERE Value = @LevelName), (SELECT Id FROM Computers WHERE Value = @ComputerName), (SELECT Id FROM Details WHERE Hash = @DetailsHash), (SELECT Id FROM Providers WHERE Value = @ProviderName), (SELECT Id FROM Channels WHERE Value = @ChannelName), @EventId, (SELECT Id FROM Mitres WHERE MitreId = @MitreId));";

    private const string SelectionDetection = @"SELECT D.Id, T.Value, D.Duration, D.Date, R.Value, L.Value, C.Value, S.Value, M.MitreId
FROM Detections AS D
INNER JOIN Titles AS T ON T.Id = D.TitleId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Details AS S ON S.Id = D.DetailsId
INNER JOIN Mitres AS M ON M.Id = D.MitreId
WHERE D.Id = @Id;";

    private const string SelectionWinEvent = @"SELECT D.Date, R.Value, C.Value, S.Value, P.Value, P.Guid, H.Value, D.EventId
FROM Detections AS D
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Details AS S ON S.Id = D.DetailsId
INNER JOIN Providers AS P ON P.Id = D.ProviderId
INNER JOIN Channels AS H ON H.Id = D.ChannelId
WHERE D.Id = @Id;";

    private long _id;

    public DetectionRepository(ILogger<DetectionRepository> logger, IHostApplicationLifetime applicationLifetime, ISettingsRepository settingsRepository, DetectionContext detectionContext, RuleContext ruleContext, IStreamingDetectionHub streamingDetectionHub, ISystemAuditService systemAuditService, IDetectionStore detectionStore, IRuleStore ruleStore, IIntegrationService integrationService, IMetricsRoot metrics)
    {
        _logger = logger;
        _detectionBlock = CreateDetectionBlock(applicationLifetime.ApplicationStopping, out var detectionLink);
        _exclusionBlock = CreateExclusionBlock(applicationLifetime.ApplicationStopping);
        _detectionContext = detectionContext;
        _ruleContext = ruleContext;
        _settingsRepository = settingsRepository;
        _streamingDetectionHub = streamingDetectionHub;
        _systemAuditService = systemAuditService;
        _detectionStore = detectionStore;
        _ruleStore = ruleStore;
        _integrationService = integrationService;
        _metrics = metrics;
        _subscription = new CompositeDisposable(detectionLink, Subscribe());
        systemAuditService.Add(new SystemAuditKey(SystemAuditType.DetectionIngestion), AuditStatus.Success);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var connection = await _detectionContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await Task.WhenAll(LoadPrimaryKeyAsync(connection.DbConnection, cancellationToken), LoadExclusionsAsync(connection.DbConnection, cancellationToken), PopulateStoreAsync(connection.DbConnection, cancellationToken));
    }
    
    public async Task<Shared.Models.Detections.Detections> GetAsync(int limit, long beforeId, long beforeDate, DetectionQuery? detectionQuery, CancellationToken cancellationToken)
    {
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var orderBy = beforeId >= 0L
            ? "D.Date DESC, D.Id DESC"
            : "D.Date ASC, D.Id ASC";

        if (detectionQuery?.Filter is null)
        {
            command.CommandText =
                $@"SELECT D.Id, T.Value, D.Duration, D.Date, R.Value, L.Value, C.Value, S.Value, M.MitreId
FROM Detections AS D INDEXED BY idx_detections_date_id
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Mitres AS M ON M.Id = D.MitreId
INNER JOIN Titles AS T ON T.Id = D.TitleId
INNER JOIN Details AS S ON S.Id = D.DetailsId
WHERE {DateFilter(beforeId, beforeDate, detectionQuery)}
GROUP BY D.Date, D.Id
ORDER BY {orderBy}
LIMIT {limit};";
        }
        else
        {
            var where = ToSqlWhere(detectionQuery);
            command.CommandText =
                $@"SELECT D.Id, T.Value, D.Duration, D.Date, R.Value, L.Value, C.Value, S.Value, M.MitreId
FROM Detections AS D INDEXED BY idx_detections_date_id
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Mitres AS M ON M.Id = D.MitreId
INNER JOIN Titles AS T ON T.Id = D.TitleId
INNER JOIN Details AS S ON S.Id = D.DetailsId
WHERE {DateFilter(beforeId, beforeDate, detectionQuery)} AND D.Id IN (
SELECT D.Id
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Mitres AS M ON M.Id = D.MitreId
WHERE {where}
)
GROUP BY D.Date, D.Id
ORDER BY {orderBy}
LIMIT {limit};";
        }

        var detections = new List<Shared.Models.Console.Responses.Detection>();
        var currentId = 0L;
        var maxRetrievedId = 0L;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        await using var reader = await command.ExecuteReaderAsync(cts.Token);
        while (await reader.ReadAsync(cts.Token))
        {
            currentId = reader.GetInt64(0);
            maxRetrievedId = Math.Max(maxRetrievedId, currentId);
            detections.Add(GetDetectionCore(reader));
        }

        return new Shared.Models.Detections.Detections(moreAvailable: beforeId >= 0L ? currentId > 0L && currentId < beforeId : maxRetrievedId > 0L && maxRetrievedId > Math.Abs(beforeId), 0, detections);

        static string DateFilter(long beforeId, long beforeDate, DetectionQuery? detectionQuery)
        {
            if (detectionQuery is null)
            {
                return beforeId >= 0L
                    ? $"(D.Date, D.Id) < ({beforeDate}, {beforeId})"
                    : $"(D.Date, D.Id) > ({Math.Abs(beforeDate)}, {Math.Abs(beforeId)})";
            }

            if (beforeId >= 0L)
            {
                if (detectionQuery is { Start: not null, End: not null })
                {
                    return $"(D.Date, D.Id) < ({beforeDate}, {beforeId}) AND (D.Date, D.Id) >= ({detectionQuery.Start.Value.Ticks}, 0) AND (D.Date, D.Id) <= ({detectionQuery.End.Value.Ticks}, {long.MaxValue})";
                }
                else if (detectionQuery is { Start: not null, End: null })
                {
                    return $"(D.Date, D.Id) < ({beforeDate}, {beforeId}) AND (D.Date, D.Id) >= ({detectionQuery.Start.Value.Ticks}, 0)";
                }
                else if (!detectionQuery.Start.HasValue && detectionQuery.End.HasValue)
                {
                    return $"(D.Date, D.Id) < ({beforeDate}, {beforeId}) AND (D.Date, D.Id) <= ({detectionQuery.End.Value.Ticks}, {long.MaxValue})";
                }

                return $"(D.Date, D.Id) < ({beforeDate}, {beforeId})";
            }
            else
            {
                if (detectionQuery is { Start: not null, End: not null })
                {
                    return $"(D.Date, D.Id) > ({Math.Abs(beforeDate)}, {Math.Abs(beforeId)}) AND (D.Date, D.Id) >= ({detectionQuery.Start.Value.Ticks}, 0) AND (D.Date, D.Id) <= ({detectionQuery.End.Value.Ticks}, {long.MaxValue})";
                }
                else if (detectionQuery is { Start: not null, End: null })
                {
                    return $"(D.Date, D.Id) > ({Math.Abs(beforeDate)}, {Math.Abs(beforeId)}) AND (D.Date, D.Id) >= ({detectionQuery.Start.Value.Ticks}, 0)";
                }
                else if (!detectionQuery.Start.HasValue && detectionQuery.End.HasValue)
                {
                    return $"(D.Date, D.Id) > ({Math.Abs(beforeDate)}, {Math.Abs(beforeId)}) AND (D.Date, D.Id) <= ({detectionQuery.End.Value.Ticks}, {long.MaxValue})";
                }

                return $"(D.Date, D.Id) > ({Math.Abs(beforeDate)}, {Math.Abs(beforeId)})";
            }
        }
    }

    public async Task<Shared.Models.Console.Responses.Detection?> GetAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectionDetection;
        command.Parameters.AddWithValue("Id", DatabaseHelper.GetValue(id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return GetDetectionCore(reader);
        }

        return null;
    }

    public async Task<WinEvent?> GetWinEventAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectionWinEvent;
        command.Parameters.AddWithValue("Id", DatabaseHelper.GetValue(id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return await GetWinEventCoreAsync(reader, cancellationToken);
        }

        return null;
    }

    public async ValueTask InsertAsync(DetectionContract detectionContract, CancellationToken cancellationToken)
    {
        _metrics.Measure.Histogram.Update(MetricOptions.DetectionDurations, new MetricTags(["ruleId"], [detectionContract.RuleId]), detectionContract.Duration);
        if (!await _detectionBlock.SendAsync(detectionContract, cancellationToken))
        {
            _logger.Throttle(nameof(DetectionRepository), itself => itself.LogError("Could not post detection"), expiration: TimeSpan.FromMinutes(1));
            _systemAuditService.Add(new SystemAuditKey(SystemAuditType.DetectionIngestion), AuditStatus.Failure);
        }
    }
    
    public async Task DeleteRulesAsync(IList<DeleteRule> rules, CancellationToken cancellationToken)
    {
        using var connection = await _detectionContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var transaction = connection.DbConnection.BeginTransaction();
        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Rules WHERE Value = @RuleId;";
            var ruleIdParameter = command.Parameters.Add("RuleId", SqliteType.Text);
            foreach (var rule in rules)
            {
                ruleIdParameter.Value = DatabaseHelper.GetValue(rule.RuleId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1) AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1) AND D.RuleId IN (SELECT Id FROM Rules WHERE Value = @RuleId);";
            var ruleIdParameter = command.Parameters.Add("RuleId", SqliteType.Text);
            foreach (var rule in rules)
            {
                ruleIdParameter.Value = DatabaseHelper.GetValue(rule.RuleId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Details WHERE Id NOT IN (SELECT DISTINCT DetailsId FROM Detections);";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        
        var ids = new HashSet<int>();
        var ruleIds = rules.Select(rule => rule.RuleId).ToHashSet();
        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.CommandText = "SELECT Id, Value FROM Exclusions;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var createRuleExclusion = JsonSerializer.Deserialize<CreateRuleExclusion>(Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(1)), SerializationContext.Default.CreateRuleExclusion);
                if (createRuleExclusion is null) continue;
                if (!ruleIds.Contains(createRuleExclusion.RuleId)) continue;
                var key = new ExclusionQueryKey(createRuleExclusion.RuleId, createRuleExclusion.Created);
                _exclusionMetricByKey.TryRemove(key, out _);
                _exclusionsByRuleId.TryRemove(createRuleExclusion.RuleId, out _);
                ids.Add(reader.GetInt32(0));
            }
        }
        
        await using (var command = connection.DbConnection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Exclusions WHERE Id = @Id;";
            var idParameter = command.Parameters.Add("Id", SqliteType.Integer);
            foreach (var id in ids)
            {
                idParameter.Value = DatabaseHelper.GetValue(id);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        
        await transaction.CommitAsync(cancellationToken);
        foreach (var rule in rules)
        {
            _detectionStore.Delete(rule.RuleId);
        }
    }

    public async Task ComputeMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _detectionContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            _metrics.Manage.ShutdownContext(MetricOptions.Detections.Context);
            foreach (var kvp in await GetDailyDetectionCountAsync(connection, cancellationToken))
            {
                var date = kvp.Key.ToLocalTime().ToString("O");
                _metrics.Measure.Counter.Increment(MetricOptions.Detections, new MetricTags(["date", "severity"], [date, Enum.GetName(DetectionSeverity.Critical)]), kvp.Value[DetectionSeverity.Critical]);
                _metrics.Measure.Counter.Increment(MetricOptions.Detections, new MetricTags(["date", "severity"], [date, Enum.GetName(DetectionSeverity.High)]), kvp.Value[DetectionSeverity.High]);
                _metrics.Measure.Counter.Increment(MetricOptions.Detections, new MetricTags(["date", "severity"], [date, Enum.GetName(DetectionSeverity.Medium)]), kvp.Value[DetectionSeverity.Medium]);
                _metrics.Measure.Counter.Increment(MetricOptions.Detections, new MetricTags(["date", "severity"], [date, Enum.GetName(DetectionSeverity.Low)]), kvp.Value[DetectionSeverity.Low]);
                _metrics.Measure.Counter.Increment(MetricOptions.Detections, new MetricTags(["date", "severity"], [date, Enum.GetName(DetectionSeverity.Informational)]), kvp.Value[DetectionSeverity.Informational]);
            }

            _metrics.Manage.ShutdownContext(MetricOptions.Computers.Context);
            foreach (var kvp in await GetDailyComputersAsync(connection, cancellationToken))
            {
                var date = kvp.Key.ToLocalTime().ToString("O");
                foreach (var computer in kvp.Value)
                {
                    _metrics.Measure.Gauge.SetValue(MetricOptions.Computers, new MetricTags(["date", "name"], [date, computer.ToLowerInvariant()]), 1);
                }
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

    private async Task<string?> GetRuleDetailsAsync(string ruleId, CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Details FROM Rules WHERE RuleId = @RuleId;";
        command.Parameters.AddWithValue("RuleId", DatabaseHelper.GetValue(ruleId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return reader.IsDBNull(0) ? null : reader.GetString(0);
        }

        return null;
    }

    private async Task InsertAsync(IList<DetectionContract> detections, CancellationToken cancellationToken)
    {
        try
        {
            if (detections.Count == 0) return;
            var today = DateTime.Today.ToString("O");
            using var connection = await _detectionContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var transaction = connection.DbConnection.BeginTransaction();

            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText = Insertion;

            var idParameter = command.Parameters.Add("Id", SqliteType.Integer);
            var titleParameter = command.Parameters.Add("Title", SqliteType.Text);
            var duration = command.Parameters.Add("Duration", SqliteType.Integer);
            var date = command.Parameters.Add("Date", SqliteType.Integer);
            var ruleGuidParameter = command.Parameters.Add("RuleGuid", SqliteType.Text);
            var levelNameParameter = command.Parameters.Add("LevelName", SqliteType.Text);
            var computerNameParameter = command.Parameters.Add("ComputerName", SqliteType.Text);
            var detailsHashParameter = command.Parameters.Add("DetailsHash", SqliteType.Text);
            var detailsValueParameter = command.Parameters.Add("DetailsValue", SqliteType.Blob);
            var providerNameParameter = command.Parameters.Add("ProviderName", SqliteType.Text);
            var providerGuidParameter = command.Parameters.Add("ProviderGuid", SqliteType.Text);
            var channelNameParameter = command.Parameters.Add("ChannelName", SqliteType.Text);
            var eventIdParameter = command.Parameters.Add("EventId", SqliteType.Integer);
            var ruleTitleParameter = command.Parameters.Add("RuleTitle", SqliteType.Text);
            var mitreIdParameter = command.Parameters.Add("MitreId", SqliteType.Text);
            var mitreTacticParameter = command.Parameters.Add("Tactic", SqliteType.Text);
            var mitreTechniqueParameter = command.Parameters.Add("Technique", SqliteType.Text);
            var mitreSubTechniqueParameter = command.Parameters.Add("SubTechnique", SqliteType.Text);
            var winEvents = new Dictionary<long, WinEvent>();
            var mitres = new Dictionary<long, Tuple<string, string, string, string>>();
            var keys = new HashSet<DetectionKey>();
            foreach (var detection in detections)
            {
                if (string.IsNullOrWhiteSpace(detection.Computer)) continue;
                var winEvent = JsonSerializer.Deserialize(detection.JsonWinEvent.Span, SerializationHelper.WinEventJsonTypeInfo);
                if (winEvent == null) continue;

                var excluded = false;
                if (_exclusionsByRuleId.TryGetValue(detection.RuleId, out var exclusions))
                {
                    foreach (var kvp in exclusions)
                    {
                        if (IsExcluded(winEvent, detection.Computer, kvp.Value))
                        {
                            var key = new ExclusionQueryKey(kvp.Value.RuleId, kvp.Value.Created);
                            _exclusionMetricByKey.AddOrUpdate(key, addValueFactory: _ => new ExclusionQueryMetric(DateTimeOffset.UtcNow, count: 1), updateValueFactory: (_, current) =>
                            {
                                current.LastExcluded = DateTimeOffset.UtcNow;
                                current.Count++;
                                return current;
                            });

                            excluded = true;
                            break;
                        }
                    }
                }

                if (excluded) continue;

                var details = string.IsNullOrWhiteSpace(detection.Details) ? detection.EventTitle : detection.Details;
                if (string.IsNullOrWhiteSpace(details)) continue;
                var detailsHash = details.ToGuid().ToString();
                var detectionKey = new DetectionKey(detection.RuleId, detection.Computer, detailsHash);
                if (!keys.Add(detectionKey)) continue;

                detailsHashParameter.Value = DatabaseHelper.GetValue(detailsHash);
                detection.Details = details;

                var severity = DetectionSeverityHelper.GetSeverity(details, severity: detection.Level.FromLevel());
                var levelName = Enum.GetName(severity)!;
                detection.Level = levelName.ToLower();

                ruleTitleParameter.Value = DatabaseHelper.GetValue(detection.Title); // Set the rule title from the detection rule metadata
                detection.Title = DetectionTitleHelper.GetTitle(details, detection.Title); // Override title if applicable
                idParameter.Value = DatabaseHelper.GetValue(Interlocked.Increment(ref _id));
                titleParameter.Value = DatabaseHelper.GetValue(detection.Title);
                duration.Value = DatabaseHelper.GetValue(detection.Duration);
                date.Value = DatabaseHelper.GetValue(detection.Date);
                ruleGuidParameter.Value = DatabaseHelper.GetValue(detection.RuleId);
                levelNameParameter.Value = DatabaseHelper.GetValue(detection.Level);
                computerNameParameter.Value = DatabaseHelper.GetValue(detection.Computer);
                detailsValueParameter.Value = DatabaseHelper.GetValue(details.LZ4CompressString());
                providerNameParameter.Value = DatabaseHelper.GetValue(winEvent.GetProviderName());
                providerGuidParameter.Value = DatabaseHelper.GetValue(winEvent.GetProviderGuid().ToString());
                channelNameParameter.Value = DatabaseHelper.GetValue(winEvent.GetChannelName());
                eventIdParameter.Value = DatabaseHelper.GetValue(winEvent.EventId);
                if (_ruleStore.TryGetMitre(detection.RuleId, out var mitreId, out var mitreTactic, out var mitreTechnique, out var mitreSubTechnique))
                {
                    detection.Mitre.Add(new MitreContract { Id = mitreId, Tactic = mitreTactic, Technique = mitreTechnique, SubTechnique = mitreSubTechnique });
                }
                
                mitreIdParameter.Value = string.IsNullOrEmpty(mitreId) ? string.Empty : mitreId;
                mitreTacticParameter.Value = string.IsNullOrEmpty(mitreTactic) ? string.Empty : mitreTactic;
                mitreTechniqueParameter.Value = string.IsNullOrEmpty(mitreTechnique) ? string.Empty : mitreTechnique;
                mitreSubTechniqueParameter.Value = string.IsNullOrEmpty(mitreSubTechnique) ? string.Empty : mitreSubTechnique;
                var rows = await command.ExecuteNonQueryAsync(cancellationToken);
                if (rows == 0) continue;
                detection.Id = _id;
                winEvents[detection.Id] = winEvent;
                mitres[detection.Id] = new Tuple<string, string, string, string>(mitreId, mitreTactic, mitreTechnique, mitreSubTechnique);
            }

            await transaction.CommitAsync(cancellationToken);
            foreach (var detection in detections)
            {
                if (detection.Id == 0) continue;
                if (winEvents.TryGetValue(detection.Id, out var winEvent) && mitres.TryGetValue(detection.Id, out var mitre))
                {
                    _detectionStore.Add(detection.RuleId, detection.Level, mitre.Item1, mitre.Item2, mitre.Item3, mitre.Item4, detection.Computer);
                    _detectionStore.Add(detection.RuleId, detection.Date);
                    _integrationService.Export(detection, winEvent, mitre.Item2, mitre.Item3, mitre.Item4);
                    detection.JsonWinEvent = ByteString.Empty;
                    _streamingDetectionHub.SendDetection(detection);
                }

                _metrics.Measure.Counter.Increment(MetricOptions.Detections, new MetricTags(["date", "severity"], [today, detection.Level]));
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

    private async Task<WinEvent> GetWinEventCoreAsync(SqliteDataReader reader, CancellationToken cancellationToken)
    {
        var date = new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero);
        var ruleId = reader.GetString(1);
        var ruleDetails = await GetRuleDetailsAsync(ruleId, cancellationToken);
        var computer = reader.GetString(2);
        var details = reader.GetFieldValue<byte[]>(3).LZ4UncompressString();
        var providerName = reader.GetString(4);
        var providerGuid = reader.GetString(5);
        var channelName = reader.GetString(6);
        var eventId = reader.GetInt32(7);
        return DetectionDetailsResolver.Resolve(providerName, providerGuid, channelName, systemTime: date.ToString("O"), computer, eventId.ToString(), ruleDetails, details);
    }

    private async Task<IDictionary<DateTime, IDictionary<DetectionSeverity, long>>> GetDailyDetectionCountAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var values = new Dictionary<DateTime, IDictionary<DetectionSeverity, long>>();
        
        var todayAsUtc = DateTime.Today.ToUniversalTime();
        var retention = await _settingsRepository.GetRetentionAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        var startDate = todayAsUtc.Subtract(retention);
        var endDate = todayAsUtc;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var detectionCountBySeverity = new Dictionary<DetectionSeverity, long>();
            foreach (var severity in Enum.GetValues<DetectionSeverity>())
            {
                command.CommandText = $@"SELECT COUNT(*)
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Mitres AS M ON M.Id = D.MitreId
WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1)
AND D.LevelId IN (SELECT Id FROM Levels WHERE Value = '{Enum.GetName(severity)?.ToLower()}')
AND D.RuleId IN (SELECT Id FROM Rules WHERE 1 = 1)
AND D.MitreId IN (SELECT Id FROM Mitres WHERE 1 = 1)
AND D.Date >= {date.Ticks} AND D.Date < {date.AddDays(1).Ticks};";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                await using var reader = await command.ExecuteReaderAsync(cts.Token);
                if (await reader.ReadAsync(cts.Token))
                {
                    detectionCountBySeverity[severity] = reader.GetInt64(0);
                }
                else
                {
                    detectionCountBySeverity[severity] = 0L;
                }
            }
            
            values[date] = detectionCountBySeverity;
        }
        
        return values;
    }

    private static Shared.Models.Console.Responses.Detection GetDetectionCore(SqliteDataReader reader)
    {
        var id = reader.GetInt64(0);
        var title = reader.GetString(1);
        var duration = new TimeSpan(reader.GetInt64(2));
        var date = new DateTimeOffset(reader.GetInt64(3), TimeSpan.Zero);
        var ruleId = reader.GetString(4);
        var level = reader.GetString(5);
        var computer = reader.GetString(6);
        var details = reader.GetFieldValue<byte[]>(7).LZ4UncompressString();
        var mitreId = reader.GetString(8);
        return new Shared.Models.Console.Responses.Detection(id, title, details, duration, date, severity: level.FromLevel(), ruleId, computer, GetMitres(mitreId).ToList());
    }

    private static string ToSqlWhere(DetectionQuery? query)
    {
        var sb = new StringBuilder();
        if (query?.Filter is null)
        {
            sb.Append("D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1) AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1) AND D.RuleId IN (SELECT Id FROM Rules WHERE 1 = 1) AND D.MitreId IN (SELECT Id FROM Mitres WHERE 1 = 1)");
            return sb.ToString();
        }
        
        if (query.Filter.Computer is not null)
        {
            sb.Append(sb.Length > 0 ? $" AND D.ComputerId IN (SELECT Id FROM Computers WHERE Value = '{query.Filter.Computer}')" : $"D.ComputerId IN (SELECT Id FROM Computers WHERE Value = '{query.Filter.Computer}')");
        }
        else
        {
            sb.Append(sb.Length > 0 ? " AND D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1)" : "D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1)");
        }
        
        if (query.Filter.Severity is not null)
        {
            sb.Append(sb.Length > 0 ? $" AND D.LevelId IN (SELECT Id FROM Levels WHERE Value = '{Enum.GetName(query.Filter.Severity.Value)?.ToLower()}')" : $"D.LevelId IN (SELECT Id FROM Levels WHERE Value = '{Enum.GetName(query.Filter.Severity.Value)?.ToLower()}')");
        }
        else
        {
            sb.Append(sb.Length > 0 ? " AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1)" : "D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1)");
        }
        
        if (query.Filter.RuleTitle is not null)
        {
            sb.Append(sb.Length > 0 ? $" AND D.RuleId IN (SELECT Id FROM Rules WHERE Title = '{query.Filter.RuleTitle}')" : $"D.RuleId IN (SELECT Id FROM Rules WHERE Title = '{query.Filter.RuleTitle}')");
        }
        else
        {
            sb.Append(sb.Length > 0 ? " AND D.RuleId IN (SELECT Id FROM Rules WHERE 1 = 1)" : "D.RuleId IN (SELECT Id FROM Rules WHERE 1 = 1)");
        }

        if (query.Filter.MitreId is not null)
        {
            if (MitreAttackResolver.Components.TryGetValue(query.Filter.MitreId, out var mitre))
            {
                if (mitre.Technique.Equals("-", StringComparison.Ordinal) && mitre.SubTechnique.Equals("-", StringComparison.Ordinal))
                {
                    sb.Append(sb.Length > 0 ? $" AND D.MitreId IN (SELECT Id FROM Mitres WHERE Tactic = '{mitre.Tactic}')" : $"D.MitreId IN (SELECT Id FROM Mitres WHERE Tactic = '{mitre.Tactic}')");
                }
                else if (!mitre.Technique.Equals("-", StringComparison.Ordinal) && mitre.SubTechnique.Equals("-", StringComparison.Ordinal))
                {
                    sb.Append(sb.Length > 0 ? $" AND D.MitreId IN (SELECT Id FROM Mitres WHERE Tactic = '{mitre.Tactic}' AND Technique = '{mitre.Technique}')" : $"D.MitreId IN (SELECT Id FROM Mitres WHERE Tactic = '{mitre.Tactic}' AND Technique = '{mitre.Technique}')");
                }
                else if (!mitre.Technique.Equals("-", StringComparison.Ordinal) && !mitre.SubTechnique.Equals("-", StringComparison.Ordinal))
                {
                    sb.Append(sb.Length > 0 ? $" AND D.MitreId IN (SELECT Id FROM Mitres WHERE Tactic = '{mitre.Tactic}' AND Technique = '{mitre.Technique}' AND SubTechnique = '{mitre.SubTechnique}')" : $"D.MitreId IN (SELECT Id FROM Mitres WHERE Tactic = '{mitre.Tactic}' AND Technique = '{mitre.Technique}' AND SubTechnique = '{mitre.SubTechnique}')");
                }
            }
        }
        else
        {
            sb.Append(sb.Length > 0 ? " AND D.MitreId IN (SELECT Id FROM Mitres WHERE 1 = 1)" : "D.MitreId IN (SELECT Id FROM Mitres WHERE 1 = 1)");
        }

        return sb.ToString();
    }

    private static IEnumerable<Mitre> GetMitres(string mitreId)
    {
        if (MitreAttackResolver.Components.TryGetValue(mitreId, out var component))
        {
            yield return new Mitre(component.Id, component.Tactic, component.Technique, component.SubTechnique);
        }
    }

    private async Task LoadPrimaryKeyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                @"
            SELECT MAX(Id)
            FROM Detections;
";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                _id = reader.IsDBNull(0) ? 0L : reader.GetInt64(0);
            }
            else
            {
                _id = 0L;
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

    private async Task PopulateStoreAsync(SqliteConnection connection, CancellationToken cancellationToken, string? ruleId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(ruleId))
            {
                _detectionStore.Delete();
            }
            else
            {
                _detectionStore.Delete(ruleId);
            }

            var watch = Stopwatch.StartNew();
            _logger.LogInformation("Populating store...");
            var activeRuleIds = await GetActiveRulesAsync(cancellationToken);
            foreach (var activeRuleId in string.IsNullOrEmpty(ruleId) ? activeRuleIds : activeRuleIds.Where(id => id.Equals(ruleId)))
            {
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"SELECT R.Value, COUNT(D.RuleId), MAX(D.Date)
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
INNER JOIN Rules AS R ON R.Id = D.RuleId
WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1) AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1) AND D.RuleId IN (SELECT Id FROM Rules WHERE Value = '{activeRuleId}');";

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    await using var reader = await command.ExecuteReaderAsync(cts.Token);
                    while (await reader.ReadAsync(cts.Token))
                    {
                        if (reader.IsDBNull(0)) continue;
                        _detectionStore.Add(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2));
                    }
                }

                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"SELECT L.Value, M.MitreId, M.Tactic, M.Technique, M.SubTechnique, C.Value, COUNT(D.Id)
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Mitres AS M ON M.Id = D.MitreId
WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1) AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1) AND D.RuleId IN (SELECT Id FROM Rules WHERE Value = '{activeRuleId}') AND D.MitreId IN (SELECT Id FROM Mitres WHERE Tactic <> '')
GROUP BY D.ComputerId;";

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    await using var reader = await command.ExecuteReaderAsync(cts.Token);
                    while (await reader.ReadAsync(cts.Token))
                    {
                        var computer = reader.GetString(5);
                        _detectionStore.Add(activeRuleId, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), computer, reader.GetInt64(6));
                    }
                }
            }

            _logger.LogInformation("Store populated in '{Time}ms'", watch.Elapsed.TotalMilliseconds);
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
    
    private async Task DeleteDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var retention = await _settingsRepository.GetRetentionAsync(cancellationToken);
            using var connection = await _detectionContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            var deletedRowCount = 0;
            await using (var transaction = connection.DbConnection.BeginTransaction())
            {
                await using (var command = connection.DbConnection.CreateCommand())
                {
                    command.CommandText = $"DELETE FROM Detections INDEXED BY idx_detections_date_id WHERE datetime(Date/10000000 - 62135596800, 'unixepoch') < DATE('now', '-{retention.TotalDays} days') RETURNING Id;";
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        deletedRowCount++;
                    }
                }

                await using (var command = connection.DbConnection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM Details WHERE Id NOT IN (SELECT DISTINCT DetailsId FROM Detections);";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }

            if (deletedRowCount > 0)
            {
                _logger.LogInformation("Deleted {Count} detections", deletedRowCount);
                await PopulateStoreAsync(connection.DbConnection, cancellationToken);
                await VacuumAsync(connection.DbConnection, cancellationToken);
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

    private IDisposable Subscribe()
    {
        return Observable.Interval(TimeSpan.FromHours(1)).Select(_ => Observable.FromAsync(async ct =>
        {
            await DeleteDataAsync(ct);
            await ComputeMetricsAsync(ct);
        })).Concat().Subscribe();
    }

    private DataFlowHelper.PeriodicBlock<DetectionContract> CreateDetectionBlock(CancellationToken cancellationToken, out IDisposable disposableLink)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            SingleProducerConstrained = false,
            BoundedCapacity = 24, // enough to cover if a single write transaction takes up to 1 minute
            CancellationToken = cancellationToken
        };

        var periodicBlock = DataFlowHelper.CreatePeriodicBlock<DetectionContract>(TimeSpan.FromSeconds(1), count: 1000);
        var options = new DataflowLinkOptions { PropagateCompletion = true };
        var propagationBlock = new ActionBlock<IList<DetectionContract>>(async items => { await InsertAsync(items, cancellationToken); }, executionDataflow);
        disposableLink = periodicBlock.LinkTo(propagationBlock, options);
        return periodicBlock;
    }

    private ActionBlock<CreateRuleExclusion> CreateExclusionBlock(CancellationToken cancellationToken)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            SingleProducerConstrained = false,
            BoundedCapacity = 12,
            CancellationToken = cancellationToken
        };

        return new ActionBlock<CreateRuleExclusion>(async createRuleExclusion => { await ProcessExclusionAsync(createRuleExclusion, cancellationToken); }, executionDataflow);
    }

    private bool _isDisposed;
    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            await SaveExclusionMetrics(CancellationToken.None);
            if (_subscription is IAsyncDisposable subscriptionAsyncDisposable)
                await subscriptionAsyncDisposable.DisposeAsync();
            else
                _subscription.Dispose();

            await _detectionBlock.DisposeAsync();
        }
    }
}