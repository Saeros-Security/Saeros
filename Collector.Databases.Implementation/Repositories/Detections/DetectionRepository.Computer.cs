using Microsoft.Data.Sqlite;
using Shared.Models.Console.Responses;

namespace Collector.Databases.Implementation.Repositories.Detections;

public sealed partial class DetectionRepository
{
    public async Task<IEnumerable<Computer>> GetComputersAsync(CancellationToken cancellationToken)
    {
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Computers;";
        var computers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var computer = reader.GetString(0);
            if (string.IsNullOrWhiteSpace(computer)) continue;
            computers.Add(computer);
        }

        return computers.Select(computer => new Computer(computer)).ToList();
    }
    
    private async Task<IDictionary<DateTime, ISet<string>>> GetDailyComputersAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var values = new Dictionary<DateTime, ISet<string>>();
        
        var todayAsUtc = DateTime.Today.ToUniversalTime();
        var retention = await _settingsRepository.GetRetentionAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        var startDate = todayAsUtc.Subtract(retention);
        var endDate = todayAsUtc;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var computers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            command.CommandText = $@"SELECT C.Value
FROM Computers AS C
WHERE C.Id IN (
SELECT D.ComputerId
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Mitres AS M ON M.Id = D.MitreId
WHERE D.ComputerId IN (SELECT Id FROM Computers WHERE 1 = 1)
AND D.LevelId IN (SELECT Id FROM Levels WHERE 1 = 1)
AND D.RuleId IN (SELECT Id FROM Rules WHERE 1 = 1)
AND D.MitreId IN (SELECT Id FROM Mitres WHERE 1 = 1)
AND D.Date >= {date.Ticks} AND D.Date < {date.AddDays(1).Ticks}
GROUP BY D.ComputerId);";
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await using var reader = await command.ExecuteReaderAsync(cts.Token);
            while (await reader.ReadAsync(cts.Token))
            {
                var computer = reader.GetString(0);
                if (string.IsNullOrWhiteSpace(computer)) continue;
                computers.Add(computer);
            }

            values[date] = computers;
        }

        return values;
    }
}