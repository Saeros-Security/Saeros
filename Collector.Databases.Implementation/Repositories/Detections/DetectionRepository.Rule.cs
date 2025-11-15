using System.Collections.Concurrent;
using Collector.Databases.Abstractions.Domain;
using Collector.Databases.Abstractions.Domain.Rules;
using Shared.Extensions;

namespace Collector.Databases.Implementation.Repositories.Detections;

public sealed partial class DetectionRepository
{
    public async Task<IEnumerable<string>> GetTodayActiveRulesAsync(CancellationToken cancellationToken)
    {
        var todayAsUtc = DateTime.Today.ToUniversalTime();
        var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $@"SELECT R.Value
FROM Rules AS R
WHERE R.Id IN (
SELECT D.RuleId
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1) AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1) AND D.RuleId IN (SELECT Id FROM Rules WHERE 1 = 1) AND D.MitreId IN (SELECT Id FROM Mitres WHERE 1 = 1) AND D.Date >= {todayAsUtc.Ticks} AND D.Date < {todayAsUtc.AddDays(1).Ticks}
GROUP BY D.ComputerId, D.LevelId, D.RuleId);";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        await using var reader = await command.ExecuteReaderAsync(cts.Token);
        while (await reader.ReadAsync(cts.Token))
        {
            if (reader.IsDBNull(0)) continue;
            ruleIds.Add(reader.GetString(0));
        }

        return ruleIds;
    }
    
    public async Task<IEnumerable<string>> GetActiveRulesAsync(CancellationToken cancellationToken)
    {
        var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Rules";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(0)) continue;
            ruleIds.Add(reader.GetString(0));
        }

        return ruleIds;
    }
    
    public async Task<IDictionary<string, long>> GetTodayRuleCountByIdsAsync(CancellationToken cancellationToken)
    {
        var todayAsUtc = DateTime.Today.ToUniversalTime();
        var ruleIds = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $@"WITH CTE AS (
SELECT D.RuleId AS RuleId, COUNT(D.Id) AS Count
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1) AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1) AND D.RuleId IN (SELECT Id FROM Rules WHERE 1 = 1) AND D.MitreId IN (SELECT Id FROM Mitres WHERE 1 = 1) AND D.Date >= {todayAsUtc.Ticks} AND D.Date < {todayAsUtc.AddDays(1).Ticks}
GROUP BY D.ComputerId, D.LevelId, D.RuleId
)

SELECT R.Value, CTE.Count
FROM Rules AS R
INNER JOIN CTE ON CTE.RuleId = R.Id;";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        await using var reader = await command.ExecuteReaderAsync(cts.Token);
        while (await reader.ReadAsync(cts.Token))
        {
            var value = reader.GetInt64(1);
            ruleIds.AddOrUpdate(reader.GetString(0), addValue: value, updateValueFactory: (_, current) => current + value);
        }

        return ruleIds;
    }
    
    public async Task<IList<RuleCountAndSeverity>> GetRuleCountAndSeverityAsync(CancellationToken cancellationToken)
    {
        var ruleCountAndSeverity = new ConcurrentDictionary<RuleCountAndSeverity, long>();
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"WITH CTE AS (
SELECT D.RuleId AS RuleId, D.LevelId AS LevelId, COUNT(D.Id) AS Count
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1) AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1) AND D.RuleId IN (SELECT Id FROM Rules WHERE 1 = 1) AND D.MitreId IN (SELECT Id FROM Mitres WHERE 1 = 1)
GROUP BY D.ComputerId, D.LevelId, D.RuleId
)

SELECT R.Title, L.Value, CTE.Count
FROM Rules AS R
INNER JOIN CTE ON CTE.RuleId = R.Id
INNER JOIN Levels AS L ON L.Id = CTE.LevelId";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        await using var reader = await command.ExecuteReaderAsync(cts.Token);
        while (await reader.ReadAsync(cts.Token))
        {
            var key = new RuleCountAndSeverity(reader.GetString(0), reader.GetString(1).FromLevel(), reader.GetInt64(2));
            ruleCountAndSeverity.AddOrUpdate(key, addValue: key.Count, updateValueFactory: (_, current) => current + key.Count);
        }

        return ruleCountAndSeverity.Keys.ToList();
    }
}