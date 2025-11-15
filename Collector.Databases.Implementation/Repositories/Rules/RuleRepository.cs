using Google.Protobuf;
using Microsoft.Data.Sqlite;
using Shared.Extensions;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Models.Detections;
using Streaming;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Collector.Core.Extensions;
using Collector.Core.Helpers;
using Collector.Core.Hubs.Rules;
using Collector.Databases.Abstractions.Domain.Rules;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Stores.Detections;
using Collector.Databases.Abstractions.Stores.Rules;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Databases.Implementation.Contexts.Rules;
using Collector.Databases.Implementation.Helpers;
using Collector.Detection.Extensions;
using Collector.Detection.Mitre;
using Collector.Detection.Rules.Extensions;
using Detection.Yaml;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Models.Rules;
using YamlDotNet.Serialization;
using Rule = Shared.Models.Console.Responses.Rule;
using Unit = System.Reactive.Unit;

namespace Collector.Databases.Implementation.Repositories.Rules;

public sealed class RuleRepository : IRuleRepository
{
    private sealed record UpdateRecord(string RuleId, DateTimeOffset Date);
    private sealed record DetectionRecord([property: YamlMember(Alias = "name")] string Name, [property: YamlMember(Alias = "detection")] Dictionary<string, object> Detection);
    private sealed record CorrelationRecord([property: YamlMember(Alias = "correlation")] Dictionary<string, object> Correlation, [property: YamlMember(Alias = "detections")] List<DetectionRecord> Detections);

    private readonly ILogger<RuleRepository> _logger;
    private readonly Subject<Unit> _insertionSubject = new();
    private readonly IDisposable _subscription;
    private readonly DataFlowHelper.PeriodicBlock<RuleContract> _insertBlock;
    private readonly DataFlowHelper.PeriodicBlock<UpdateRecord> _updateBlock;
    private readonly RuleContext _ruleContext;
    private readonly ISettingsStore _settingsStore;
    private readonly IDetectionStore _detectionStore;
    private readonly IRuleStore _ruleStore;
    private readonly IStreamingRuleHub _streamingRuleHub;
    private static readonly TimeSpan InsertionInterval = TimeSpan.FromSeconds(1);
    private const string RepeatedFieldSeparator = ", ";

    private const string Insertion = @"INSERT OR REPLACE INTO Rules (RuleId, Title, Author, Details, Description, Date, Modified, Level, Reference, FalsePositives, Tags, Status, CorrelationOrAggregationTimeSpan, DetectionCount, Updated, Builtin, Enabled, Content, GroupName, MitreId, MitreTactic, MitreTechnique, MitreSubTechnique, Volume, Source)
VALUES (@RuleId, @Title, @Author, @Details, @Description, @Date, @Modified, @Level, @Reference, @FalsePositives, @Tags, @Status, @CorrelationOrAggregationTimeSpan, coalesce((SELECT DetectionCount FROM Rules WHERE Title = @Title), 0), coalesce((SELECT Updated FROM Rules WHERE Title = @Title), NULL), @Builtin, coalesce((SELECT Enabled FROM Rules WHERE Title = @Title), @Enabled), @Content, coalesce(@GroupName, (SELECT GroupName FROM Rules WHERE Title = @Title)), @MitreId, @MitreTactic, @MitreTechnique, @MitreSubTechnique, @Volume, @Source);";

    private const string Selection = @"SELECT RuleId, Title, Author, Details, Description, Date, Modified, Level, Reference, FalsePositives, Tags, Status, CorrelationOrAggregationTimeSpan, DetectionCount, Updated, Builtin, Enabled, GroupName, Volume, Source
FROM Rules;";

    public RuleRepository(ILogger<RuleRepository> logger, RuleContext ruleContext, ISettingsStore settingsStore, IDetectionStore detectionStore, IRuleStore ruleStore, IStreamingRuleHub streamingRuleHub, IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _ruleContext = ruleContext;
        _settingsStore = settingsStore;
        _detectionStore = detectionStore;
        _ruleStore = ruleStore;
        _streamingRuleHub = streamingRuleHub;
        _insertBlock = CreateBlock<RuleContract>(applicationLifetime.ApplicationStopping, InsertAsync, period: InsertionInterval, out var insertionLink);
        _updateBlock = CreateBlock<UpdateRecord>(applicationLifetime.ApplicationStopping, UpdateAsync, period: TimeSpan.FromSeconds(25), out var updateLink);
        _subscription = new CompositeDisposable(insertionLink, updateLink);
    }
    
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ruleStore.Delete();
        await foreach (var record in EnumerateEnabledRuleIdsAsync(cancellationToken))
        {
            _ruleStore.Add(record);
        }
        
        await foreach (var record in EnumerateDisabledRuleIdsAsync(cancellationToken))
        {
            _ruleStore.Add(record);
        }
    }

    public async Task<Shared.Models.Rules.Rules> GetAsync(CancellationToken cancellationToken)
    {
        var rules = new List<RuleItem>();
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = Selection;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var contract = Read(reader, readContent: false);
            rules.Add(new RuleItem(contract.Id,
                contract.Title,
                contract.Date,
                contract.HasModified ? contract.Modified : null,
                contract.Author,
                contract.HasDetails ? contract.Details : null,
                contract.HasDescription ? contract.Description : null,
                contract.Level,
                contract.Status,
                contract.Tags,
                contract.References,
                contract.FalsePositives,
                contract.HasCorrelationOrAggregationTimeSpan ? TimeSpan.FromTicks(contract.CorrelationOrAggregationTimeSpan) : null,
                contract.DetectionCount,
                contract.HasUpdated ? new DateTimeOffset(contract.Updated, TimeSpan.Zero) : null,
                contract.Builtin,
                contract.Enabled,
                contract.Content.ToByteArray(),
                contract.GroupName,
                contract.Mitre.Select(mitre => new Mitre(mitre.Id, mitre.Tactic, mitre.Technique, mitre.SubTechnique)),
                contract.Source,
                (AuditPolicyVolume)contract.Volume));
        }

        return new Shared.Models.Rules.Rules(rules);
    }

    public async Task<Rule?> GetAsync(string ruleId, CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Title, Author, Description, Level, DetectionCount, Date, Updated, Builtin, Enabled, GroupName, Tags, Volume, Source FROM Rules WHERE RuleId = @RuleId;";
        command.Parameters.AddWithValue("RuleId", ruleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var title = reader.GetString(0);
            var author = reader.GetString(1);
            var description = reader.IsDBNull(2) ? null : reader.GetString(2);
            var level = reader.GetString(3);
            var detectionCount = reader.GetInt64(4);
            var date = reader.GetString(5);
            long? updated = reader.IsDBNull(6) ? null : reader.GetInt64(6);
            var builtin = reader.GetBoolean(7);
            var enabled = reader.GetBoolean(8);
            var groupName = reader.GetString(9);
            var tags = reader.IsDBNull(10) ? [] : reader.GetString(10).Split(RepeatedFieldSeparator, StringSplitOptions.RemoveEmptyEntries);
            var volume = reader.GetInt32(11);
            var source = reader.GetString(12);
            return new Rule(ruleId, title, description, level.FromLevel(), detectionCount, date, updated, author, builtin, (AuditPolicyVolume)volume, enabled, groupName, MitreExtensions.GetMitre(tags, mitre => new Mitre(mitre.Id, mitre.Tactic, mitre.Technique, mitre.SubTechnique)), source);
        }

        return null;
    }

    public async Task<Rule?> GetByTitleAsync(string title, CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RuleId, Author, Description, Level, DetectionCount, Date, Updated, Builtin, Enabled, GroupName, Tags, Volume, Source FROM Rules WHERE Title = @Title;";
        command.Parameters.AddWithValue("Title", title);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var ruleId = reader.GetString(0);
            var author = reader.GetString(1);
            var description = reader.IsDBNull(2) ? null : reader.GetString(2);
            var level = reader.GetString(3);
            var detectionCount = reader.GetInt64(4);
            var date = reader.GetString(5);
            long? updated = reader.IsDBNull(6) ? null : reader.GetInt64(6);
            var builtin = reader.GetBoolean(7);
            var enabled = reader.GetBoolean(8);
            var groupName = reader.GetString(9);
            var tags = reader.IsDBNull(10) ? [] : reader.GetString(10).Split(RepeatedFieldSeparator, StringSplitOptions.RemoveEmptyEntries);
            var volume = reader.GetInt32(11);
            var source = reader.GetString(12);
            return new Rule(ruleId, title, description, level.FromLevel(), detectionCount, date, updated, author, builtin, (AuditPolicyVolume)volume, enabled, groupName, MitreExtensions.GetMitre(tags, mitre => new Mitre(mitre.Id, mitre.Tactic, mitre.Technique, mitre.SubTechnique)), source);
        }

        return null;
    }

    public async Task<IEnumerable<RuleTitle>> GetRuleTitlesAsync(CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Title FROM Rules;";
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            titles.Add(reader.GetString(0));
        }

        return titles.Select(title => new RuleTitle(title)).ToList();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Rules;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return reader.GetInt32(0);
        }

        return 0;
    }

    public async Task<RuleAttributes?> GetAttributesAsync(string ruleId, CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Author, Description, Level, Date, Enabled, DetectionCount, Reference, FalsePositives, Tags, Content FROM Rules WHERE RuleId = @RuleId;";
        command.Parameters.AddWithValue("RuleId", ruleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var author = reader.GetString(0);
            var description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var level = reader.GetString(2);
            var created = reader.GetString(3);
            var enabled = reader.GetBoolean(4);
            var detectionCount = reader.GetInt64(5);
            var references = reader.IsDBNull(6) ? [] : reader.GetString(6).Split(RepeatedFieldSeparator, StringSplitOptions.RemoveEmptyEntries);
            var falsePositives = reader.IsDBNull(7) ? [] : reader.GetString(7).Split(RepeatedFieldSeparator, StringSplitOptions.RemoveEmptyEntries);
            var tags = reader.IsDBNull(8) ? [] : reader.GetString(8).Split(RepeatedFieldSeparator, StringSplitOptions.RemoveEmptyEntries);
            var yamlContent = reader.GetFieldValue<byte[]>(9);
            var yamlRules = YamlParser.DeserializeMany<YamlRule>(Encoding.UTF8.GetString(yamlContent)).ToList();
            var correlationRule = yamlRules.SingleOrDefault(rule => rule.IsCorrelation());
            if (correlationRule is not null)
            {
                var yaml = YamlParser.Serialize(new CorrelationRecord(correlationRule.Correlations, yamlRules.Where(detection => detection.Detections.Count > 0).Select(detection => new DetectionRecord(detection.Name, detection.Detections)).ToList()));
                return new RuleAttributes(author, description, level, created, enabled, detectionCount, references, falsePositives, tags, Encoding.UTF8.GetBytes(yaml));
            }
            else
            {
                var rule = yamlRules.Single();
                var yaml = YamlParser.Serialize(rule.Detections);
                return new RuleAttributes(author, description, level, created, enabled, detectionCount, references, falsePositives, tags, Encoding.UTF8.GetBytes(yaml));
            }
        }

        return null;
    }

    public async Task<string> UpdateRuleCodeAsync(string ruleId, string code, CancellationToken cancellationToken)
    {
        using var connection = await _ruleContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText = "SELECT Content FROM Rules WHERE RuleId = @RuleId;";
        command.Parameters.AddWithValue("RuleId", ruleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var yamlContent = reader.GetFieldValue<byte[]>(0);
            var yamlRules = YamlParser.DeserializeMany<YamlRule>(Encoding.UTF8.GetString(yamlContent)).ToList();
            var correlationRule = yamlRules.SingleOrDefault(rule => rule.IsCorrelation());
            using var textReader = new StringReader(code);
            if (correlationRule is not null)
            {
                correlationRule.Correlations = YamlParser.Deserialize<Dictionary<string, object>>(textReader);
                return YamlParser.Serialize(correlationRule);
            }
            else
            {
                var rule = yamlRules.Single();
                rule.Detections = YamlParser.Deserialize<Dictionary<string, object>>(textReader);
                return YamlParser.Serialize(rule);
            }
        }

        return string.Empty;
    }

    public async IAsyncEnumerable<RuleContentRecord> EnumerateNonBuiltinRulesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Content, Enabled, GroupName FROM Rules WHERE Builtin = 0;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new RuleContentRecord(reader.GetFieldValue<byte[]>(0), Enabled: reader.GetBoolean(1), GroupName: reader.GetString(2));
        }
    }

    public async IAsyncEnumerable<RuleRecord> EnumerateEnabledRuleIdsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RuleId, Description, MitreId, MitreTactic, MitreTechnique, MitreSubTechnique FROM Rules WHERE Enabled = 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var ruleId = reader.GetString(0);
            var description = reader.GetString(1);
            var mitreId = reader.IsDBNull(2) ? null : reader.GetString(2);
            var mitreTactic = reader.IsDBNull(3) ? null : reader.GetString(3);
            var mitreTechnique = reader.IsDBNull(4) ? null : reader.GetString(4);
            var mitreSubTechnique = reader.IsDBNull(5) ? null : reader.GetString(5);
            yield return new RuleRecord(ruleId, description, mitreId, mitreTactic, mitreTechnique, mitreSubTechnique);
        }
    }

    public async IAsyncEnumerable<RuleRecord> EnumerateDisabledRuleIdsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RuleId, Description, MitreId, MitreTactic, MitreTechnique, MitreSubTechnique FROM Rules WHERE Enabled = 0;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var ruleId = reader.GetString(0);
            var description = reader.GetString(1);
            var mitreId = reader.IsDBNull(2) ? null : reader.GetString(2);
            var mitreTactic = reader.IsDBNull(3) ? null : reader.GetString(3);
            var mitreTechnique = reader.IsDBNull(4) ? null : reader.GetString(4);
            var mitreSubTechnique = reader.IsDBNull(5) ? null : reader.GetString(5);
            yield return new RuleRecord(ruleId, description, mitreId, mitreTactic, mitreTechnique, mitreSubTechnique);
        }
    }

    public async Task<string?> GetRuleContentAsync(string ruleId, CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Content FROM Rules WHERE RuleId = @RuleId;";
        command.Parameters.AddWithValue("RuleId", ruleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0));
        }

        return null;
    }

    public async ValueTask InsertAsync(RuleContract ruleContract, CancellationToken cancellationToken)
    {
        if (!await _insertBlock.SendAsync(ruleContract, cancellationToken))
        {
            _logger.Throttle(nameof(RuleRepository), itself => itself.LogError("Could not insert rule"), expiration: TimeSpan.FromMinutes(1));
        }
    }

    public async ValueTask UpdateAsync(string ruleId, DateTimeOffset date, CancellationToken cancellationToken)
    {
        if (!await _updateBlock.SendAsync(new UpdateRecord(ruleId, date), cancellationToken))
        {
            _logger.Throttle(nameof(RuleRepository), itself => itself.LogError("Could not update rule"), expiration: TimeSpan.FromMinutes(1));
        }
    }

    public async Task<(byte[] Content, bool Enabled)> CopyRuleAsync(string ruleId, string ruleTitle, string groupName, CancellationToken cancellationToken)
    {
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("RuleId", ruleId);
        command.CommandText = "SELECT RuleId, Title, Author, Details, Description, Date, Modified, Level, Reference, FalsePositives, Tags, Status, CorrelationOrAggregationTimeSpan, DetectionCount, Updated, Builtin, Enabled, Content, GroupName, Volume, Source FROM Rules WHERE RuleId = @RuleId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var ruleContract = Read(reader, readContent: true);
            ruleContract.Id = Guid.NewGuid().ToString();
            ruleContract.Title = ruleTitle;
            ruleContract.GroupName = groupName;
            ruleContract.DetectionCount = 0;
            ruleContract.ClearUpdated();
            ruleContract.Builtin = false;
            var yamlRules = YamlParser.DeserializeMany<YamlRule>(Encoding.UTF8.GetString(ruleContract.Content.ToByteArray())).ToList();
            var correlationRule = yamlRules.SingleOrDefault(rule => rule.IsCorrelation());
            if (correlationRule is not null)
            {
                correlationRule.Id = ruleContract.Id;
                correlationRule.Title = ruleContract.Title;
                var sb = new StringBuilder();
                sb.Append(YamlParser.Serialize(correlationRule));
                sb.AppendLine();
                foreach (var yamlRule in yamlRules.Where(rule => !rule.IsCorrelation()))
                {
                    sb.Append("---");
                    sb.AppendLine();
                    sb.Append(YamlParser.Serialize(yamlRule));
                    sb.AppendLine();
                }

                ruleContract.Content = ByteString.CopyFromUtf8(sb.ToString());
            }
            else
            {
                var yamlRule = yamlRules.Single();
                yamlRule.Id = ruleContract.Id;
                yamlRule.Title = ruleContract.Title;
                ruleContract.Content = ByteString.CopyFromUtf8(YamlParser.Serialize(yamlRule));
            }

            await InsertAsync(new List<RuleContract> { ruleContract }, cancellationToken);
            return (ruleContract.Content.ToByteArray(), ruleContract.Enabled);
        }

        throw new Exception("Could not copy rule: Rule not found");
    }

    public async Task EnableAsync(IList<EnableRule> rules, CancellationToken cancellationToken)
    {
        if (rules.Count == 0) return;
        using var connection = await _ruleContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var transaction = connection.DbConnection.BeginTransaction();
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText = "UPDATE Rules SET Enabled = 1 WHERE RuleId = @RuleId;";
        var ruleIdParameter = command.Parameters.Add("RuleId", SqliteType.Text);
        foreach (var rule in rules)
        {
            ruleIdParameter.Value = DatabaseHelper.GetValue(rule.RuleId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DisableAsync(IList<DisableRule> rules, CancellationToken cancellationToken)
    {
        if (rules.Count == 0) return;
        using var connection = await _ruleContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var transaction = connection.DbConnection.BeginTransaction();
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText = "UPDATE Rules SET Enabled = 0 WHERE RuleId = @RuleId;";
        var ruleIdParameter = command.Parameters.Add("RuleId", SqliteType.Text);
        foreach (var rule in rules)
        {
            ruleIdParameter.Value = DatabaseHelper.GetValue(rule.RuleId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(IList<DeleteRule> rules, CancellationToken cancellationToken)
    {
        using var connection = await _ruleContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var transaction = connection.DbConnection.BeginTransaction();
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText = "DELETE FROM Rules WHERE RuleId = @RuleId;";
        var ruleIdParameter = command.Parameters.Add("RuleId", SqliteType.Text);
        foreach (var rule in rules)
        {
            ruleIdParameter.Value = DatabaseHelper.GetValue(rule.RuleId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _detectionStore.Delete(rule.RuleId);
            _ruleStore.Delete(rule.RuleId);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public IEnumerable<Mitre> GetMitres()
    {
        return MitreAttackResolver.Components.Select(kvp => new Mitre(kvp.Value.Id, kvp.Value.Tactic, kvp.Value.Technique, kvp.Value.SubTechnique));
    }

    public bool TryGetDescription(string ruleId, [MaybeNullWhen(false)] out string description)
    {
        return _ruleStore.TryGetDescription(ruleId, out description);
    }

    public async Task EnableRulesAsync(CancellationToken cancellationToken)
    {
        var enabledRules = new HashSet<string>();
        var disabledRules = new HashSet<string>();
        await using (var connection = _ruleContext.CreateConnection())
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT RuleId, Level, Status, Volume FROM Rules;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rules = new List<Tuple<string, DetectionSeverity, DetectionStatus, AuditPolicyVolume>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                rules.Add(new Tuple<string, DetectionSeverity, DetectionStatus, AuditPolicyVolume>(reader.GetString(0), reader.GetString(1).FromLevel(), reader.GetString(2).FromStatus(), (AuditPolicyVolume)reader.GetInt32(3)));
            }
            
            foreach (var rule in rules)
            {
                if (ProfileHelper.ShouldBeEnabled(rule.Item1, _settingsStore.Profile, rule.Item2, rule.Item3, rule.Item4))
                {
                    enabledRules.Add(rule.Item1);
                }
                else
                {
                    disabledRules.Add(rule.Item1);
                }
            }
        }

        await EnableAsync(enabledRules.Select(rule => new EnableRule(rule)).ToList(), cancellationToken);
        await DisableAsync(disabledRules.Select(rule => new DisableRule(rule)).ToList(), cancellationToken);
    }

    public async Task<IDictionary<DetectionSeverity, SortedSet<string>>> GetRuleTitlesBySeverityAsync(CancellationToken cancellationToken)
    {
        var ruleTitlesBySeverity = new Dictionary<DetectionSeverity, SortedSet<string>>();
        await using var connection = _ruleContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Title, Level FROM Rules;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var title = reader.GetString(0);
            var severity = reader.GetString(1).FromLevel();
            if (ruleTitlesBySeverity.TryGetValue(severity, out var titles))
            {
                titles.Add(title);
            }
            else
            {
                ruleTitlesBySeverity.Add(severity, [title]);
            }
        }

        return ruleTitlesBySeverity;
    }

    public IObservable<Unit> RuleInsertionObservable => _insertionSubject.Throttle(InsertionInterval * 2);

    private async Task InsertAsync(IList<RuleContract> rules, CancellationToken cancellationToken)
    {
        try
        {
            if (rules.Count == 0) return;
            using var connection = await _ruleContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var transaction = connection.DbConnection.BeginTransaction();
            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText = Insertion;

            var ruleId = command.Parameters.Add("RuleId", SqliteType.Text);
            var title = command.Parameters.Add("Title", SqliteType.Text);
            var author = command.Parameters.Add("Author", SqliteType.Text);
            var details = command.Parameters.Add("Details", SqliteType.Text);
            var description = command.Parameters.Add("Description", SqliteType.Text);
            var date = command.Parameters.Add("Date", SqliteType.Text);
            var modified = command.Parameters.Add("Modified", SqliteType.Text);
            var level = command.Parameters.Add("Level", SqliteType.Text);
            var reference = command.Parameters.Add("Reference", SqliteType.Text);
            var falsePositives = command.Parameters.Add("FalsePositives", SqliteType.Text);
            var tags = command.Parameters.Add("Tags", SqliteType.Text);
            var status = command.Parameters.Add("Status", SqliteType.Text);
            var correlationOrAggregationTimeSpan = command.Parameters.Add("CorrelationOrAggregationTimeSpan", SqliteType.Integer);
            var builtin = command.Parameters.Add("Builtin", SqliteType.Integer);
            var enabled = command.Parameters.Add("Enabled", SqliteType.Integer);
            var content = command.Parameters.Add("Content", SqliteType.Blob);
            var groupName = command.Parameters.Add("GroupName", SqliteType.Text);
            var mitreId = command.Parameters.Add("MitreId", SqliteType.Text);
            var mitreTactic = command.Parameters.Add("MitreTactic", SqliteType.Text);
            var mitreTechnique = command.Parameters.Add("MitreTechnique", SqliteType.Text);
            var mitreSubTechnique = command.Parameters.Add("MitreSubTechnique", SqliteType.Text);
            var volume = command.Parameters.Add("Volume", SqliteType.Integer);
            var source = command.Parameters.Add("Source", SqliteType.Text);
            foreach (var rule in rules)
            {
                ruleId.Value = DatabaseHelper.GetValue(rule.Id);
                title.Value = DatabaseHelper.GetValue(rule.Title);
                author.Value = DatabaseHelper.GetValue(rule.Author);
                details.Value = DatabaseHelper.GetValue(rule.HasDetails ? rule.Details : null);
                description.Value = DatabaseHelper.GetValue(rule.HasDescription ? rule.Description : null);
                date.Value = DatabaseHelper.GetValue(rule.Date);
                modified.Value = DatabaseHelper.GetValue(rule.HasModified ? rule.Modified : null);
                level.Value = DatabaseHelper.GetValue(rule.Level);
                reference.Value = DatabaseHelper.GetValue(string.Join(RepeatedFieldSeparator, rule.References));
                falsePositives.Value = DatabaseHelper.GetValue(string.Join(RepeatedFieldSeparator, rule.FalsePositives));
                tags.Value = DatabaseHelper.GetValue(string.Join(RepeatedFieldSeparator, rule.Tags));
                status.Value = DatabaseHelper.GetValue(rule.Status);
                correlationOrAggregationTimeSpan.Value = DatabaseHelper.GetValue(rule.HasCorrelationOrAggregationTimeSpan ? rule.CorrelationOrAggregationTimeSpan : null);
                builtin.Value = DatabaseHelper.GetValue(rule.Builtin);
                enabled.Value = !rule.Enabled ? DatabaseHelper.GetValue(rule.Enabled) : DatabaseHelper.GetValue(ProfileHelper.ShouldBeEnabled(rule.Id, _settingsStore.Profile, rule.Level.FromLevel(), rule.Status.FromStatus(), (AuditPolicyVolume)rule.Volume));
                content.Value = DatabaseHelper.GetValue(rule.Content.ToByteArray());
                groupName.Value = DatabaseHelper.GetValue(rule.GroupName);
                var mitre = rule.Mitre.SingleOrDefault();
                if (mitre is not null)
                {
                    mitreId.Value = DatabaseHelper.GetValue(mitre.Id);
                    mitreTactic.Value = DatabaseHelper.GetValue(mitre.Tactic);
                    mitreTechnique.Value = DatabaseHelper.GetValue(mitre.Technique);
                    mitreSubTechnique.Value = DatabaseHelper.GetValue(mitre.SubTechnique);
                }
                else
                {
                    mitreId.Value = DBNull.Value;
                    mitreTactic.Value = DBNull.Value;
                    mitreTechnique.Value = DBNull.Value;
                    mitreSubTechnique.Value = DBNull.Value;
                }

                volume.Value = DatabaseHelper.GetValue(rule.Volume);
                source.Value = DatabaseHelper.GetValue(rule.Source);
                await command.ExecuteNonQueryAsync(cancellationToken);
                _insertionSubject.OnNext(Unit.Default);
                _ruleStore.Add(new RuleRecord(rule.Id, rule.Description, mitre?.Id, mitre?.Tactic, mitre?.Technique, mitre?.SubTechnique));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _ruleContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _ruleContext.Logger.LogError(ex, "An error has occurred");
        }
    }

    private async Task UpdateAsync(IList<UpdateRecord> updates, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await _ruleContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var transaction = connection.DbConnection.BeginTransaction();
            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText = "UPDATE Rules SET DetectionCount = @DetectionCount, Updated = coalesce(@Updated, (SELECT Updated FROM Rules WHERE RuleId = @RuleId)) WHERE RuleId = @RuleId;";

            var ruleId = command.Parameters.Add("RuleId", SqliteType.Text);
            var updated = command.Parameters.Add("Updated", SqliteType.Integer);
            var detectionCount = command.Parameters.Add("DetectionCount", SqliteType.Integer);
            var updatedByRuleId = updates.GroupBy(update => update.RuleId).ToDictionary(kvp => kvp.Key, kvp => kvp.Select(update => update.Date.Ticks).OrderDescending().First());
            var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _detectionStore.DetectionCountByRuleId)
            {
                ruleId.Value = DatabaseHelper.GetValue(kvp.Key);
                detectionCount.Value = DatabaseHelper.GetValue(kvp.Value.Count);
                if (kvp.Value.Count > 0)
                {
                    updated.Value = updatedByRuleId.TryGetValue(kvp.Key, out var value) ? DatabaseHelper.GetValue(value) : (kvp.Value.Count > 0 ? kvp.Value.Updated : DBNull.Value);
                }
                else
                {
                    updated.Value = 0;
                }
                
                await command.ExecuteNonQueryAsync(cancellationToken);
                ruleIds.Add(kvp.Key);
            }

            await foreach (var id in EnumerateRuleIdsAsync(connection.DbConnection, cancellationToken))
            {
                if (ruleIds.Contains(id)) continue;
                ruleId.Value = DatabaseHelper.GetValue(id);
                detectionCount.Value = DatabaseHelper.GetValue(0);
                updated.Value = 0;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            await foreach (var rule in EnumerateAsync(connection.DbConnection, updatedByRuleId.Keys.ToHashSet(), cancellationToken))
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                _streamingRuleHub.SendRule(rule); // Use sync because we do not need to wait for a client to be online
            }
        }
        catch (OperationCanceledException)
        {
            _ruleContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            _ruleContext.Logger.LogError(ex, "An error has occurred");
        }
    }
    
    private static async IAsyncEnumerable<string> EnumerateRuleIdsAsync(SqliteConnection connection, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RuleId FROM Rules;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return reader.GetString(0);
        }
    }

    private static async IAsyncEnumerable<RuleContract> EnumerateAsync(SqliteConnection connection, ISet<string> ruleIds, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Parameters.AddRange(ruleIds.Select((p, index) => new SqliteParameter("@p" + index, p)));
        command.CommandText = $@"SELECT RuleId, Title, Author, Details, Description, Date, Modified, Level, Reference, FalsePositives, Tags, Status, CorrelationOrAggregationTimeSpan, DetectionCount, Updated, Builtin, Enabled, GroupName, Volume, Source
FROM Rules
WHERE RuleId IN ({string.Join(",", ruleIds.Select((_, index) => "@p" + index))});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return Read(reader, readContent: false);
        }
    }

    private static RuleContract Read(SqliteDataReader reader, bool readContent)
    {
        var rule = new RuleContract
        {
            Id = reader.GetString(0),
            Title = reader.GetString(1),
            Author = reader.GetString(2),
            Date = reader.GetString(5),
            Level = reader.GetString(7),
            Status = reader.GetString(11),
            DetectionCount = reader.GetInt64(13)
        };

        if (!reader.IsDBNull(3))
        {
            rule.Details = reader.GetString(3);
        }

        if (!reader.IsDBNull(4))
        {
            rule.Description = reader.GetString(4);
        }

        if (!reader.IsDBNull(6))
        {
            rule.Modified = reader.GetString(6);
        }

        if (!reader.IsDBNull(8))
        {
            rule.References.AddRange(reader.GetString(8).Split(RepeatedFieldSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        if (!reader.IsDBNull(9))
        {
            rule.FalsePositives.AddRange(reader.GetString(9).Split(RepeatedFieldSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        if (!reader.IsDBNull(10))
        {
            rule.Tags.AddRange(reader.GetString(10).Split(RepeatedFieldSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        if (!reader.IsDBNull(12))
        {
            rule.CorrelationOrAggregationTimeSpan = reader.GetInt64(12);
        }

        if (!reader.IsDBNull(14))
        {
            rule.Updated = reader.GetInt64(14);
        }

        rule.Builtin = reader.GetBoolean(15);
        rule.Enabled = reader.GetBoolean(16);
        rule.Content = readContent ? ByteString.CopyFrom(reader.GetFieldValue<byte[]>(17)) : ByteString.Empty;
        rule.GroupName = readContent ? reader.GetString(18) : reader.GetString(17);
        rule.Volume = readContent ? reader.GetInt32(19) : reader.GetInt32(18);
        rule.Mitre.AddRange(rule.GetMitre(rule.Tags, mitre => new MitreContract { Id = mitre.Id, Tactic = mitre.Tactic, Technique = mitre.Technique, SubTechnique = mitre.SubTechnique }));
        rule.Source = readContent ? reader.GetString(20) : reader.GetString(19);
        return rule;
    }

    private static DataFlowHelper.PeriodicBlock<T> CreateBlock<T>(CancellationToken cancellationToken, Func<IList<T>, CancellationToken, Task> action, TimeSpan period, out IDisposable disposableLink)
    {
        var executionDataflow = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            SingleProducerConstrained = false,
            BoundedCapacity = 2,
            CancellationToken = cancellationToken
        };

        var periodicBlock = DataFlowHelper.CreatePeriodicBlock<T>(period);
        var options = new DataflowLinkOptions { PropagateCompletion = true };
        var propagationBlock = new ActionBlock<IList<T>>(async items => { await action(items, cancellationToken); }, executionDataflow);
        disposableLink = periodicBlock.LinkTo(propagationBlock, options);
        return periodicBlock;
    }

    public async ValueTask DisposeAsync()
    {
        _insertionSubject.Dispose();
        if (_subscription is IAsyncDisposable subscriptionAsyncDisposable)
            await subscriptionAsyncDisposable.DisposeAsync();
        else
            _subscription.Dispose();
        
        await _insertBlock.DisposeAsync();
        await _updateBlock.DisposeAsync();
    }
}