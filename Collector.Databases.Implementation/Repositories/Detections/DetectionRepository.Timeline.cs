using Microsoft.Data.Sqlite;
using Shared.Extensions;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Models.Detections;

namespace Collector.Databases.Implementation.Repositories.Detections;

public partial class DetectionRepository
{
    public async Task<Timeline> GetTimelineAsync(DetectionQuery detectionQuery, int step, CancellationToken cancellationToken)
    {
        await using var connection = _detectionContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var where = ToSqlWhere(detectionQuery);
        switch (detectionQuery)
        {
            case { Start: not null, End: not null }:
                return await ComputeTimelineAsync(detectionQuery.Start.Value, detectionQuery.End.Value, command, where, step, cancellationToken);
            case { Start: not null, End: null }:
                return await ComputeTimelineAsync(detectionQuery.Start.Value, DateTimeOffset.UtcNow, command, where, step, cancellationToken);
            default:
            {
                if (!detectionQuery.Start.HasValue && detectionQuery.End.HasValue)
                {
                    var end = detectionQuery.End.Value;
                    return await ComputeTimelineAsync(end - TimeSpan.FromDays(1), end, command, where, step, cancellationToken);
                }

                var now = DateTimeOffset.UtcNow;
                return await ComputeTimelineAsync(now - TimeSpan.FromDays(1), now, command, where, step, cancellationToken);
            }
        }

        static IEnumerable<Tuple<DateTimeOffset, DateTimeOffset>> SplitDateRange(DateTimeOffset start, DateTimeOffset end, int count)
        {
            var interval = (end - start) / count;
            return Enumerable.Range(0, count).Select(i =>
            {
                var step = start.Ticks + i * interval.Ticks;
                var floor = new DateTimeOffset(step, TimeSpan.Zero);
                var ceil = new DateTimeOffset(step + interval.Ticks, TimeSpan.Zero);
                return Tuple.Create(floor, i == count - 1 ? end : ceil);
            });
        }
        
        static async Task<Timeline> ComputeTimelineAsync(DateTimeOffset start, DateTimeOffset end, SqliteCommand command, string where, int step, CancellationToken cancellationToken)
        {
            var information = new List<Column>();
            var low = new List<Column>();
            var medium = new List<Column>();
            var high = new List<Column>();
            var critical = new List<Column>();
            foreach (var sequence in GetSequences(start, end, step))
            {
                command.CommandText =
                    $@"SELECT COUNT(D.Id), L.Value
FROM Detections AS D INDEXED BY idx_detections_computer_level_rule_mitre_date
INNER JOIN Computers AS C ON C.Id = D.ComputerId
INNER JOIN Levels AS L ON L.Id = D.LevelId
INNER JOIN Rules AS R ON R.Id = D.RuleId
INNER JOIN Mitres AS M ON M.Id = D.MitreId
WHERE {where}
AND {DateFilter(sequence.Item1, sequence.Item2)}
GROUP BY L.Value;";
                
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                await using var reader = await command.ExecuteReaderAsync(cts.Token);
                var informationFilled = false;
                var lowFilled = false;
                var mediumFilled = false;
                var highFilled = false;
                var criticalFilled = false;
                while (await reader.ReadAsync(cts.Token))
                {
                    var count = reader.GetInt64(0);
                    var level = reader.GetString(1);
                    switch (level.FromLevel())
                    {
                        case DetectionSeverity.Informational:
                            informationFilled = true;
                            information.Add(new Column(sequence.Item1.Ticks, count));
                            break;
                        case DetectionSeverity.Low:
                            lowFilled = true;
                            low.Add(new Column(sequence.Item1.Ticks, count));
                            break;
                        case DetectionSeverity.Medium:
                            mediumFilled = true;
                            medium.Add(new Column(sequence.Item1.Ticks, count));
                            break;
                        case DetectionSeverity.High:
                            highFilled = true;
                            high.Add(new Column(sequence.Item1.Ticks, count));
                            break;
                        case DetectionSeverity.Critical:
                            criticalFilled = true;
                            critical.Add(new Column(sequence.Item1.Ticks, count));
                            break;
                    }
                }

                if (!informationFilled)
                {
                    information.Add(new Column(sequence.Item1.Ticks, count: 0));
                }
                
                if (!lowFilled)
                {
                    low.Add(new Column(sequence.Item1.Ticks, count: 0));
                }
                
                if (!mediumFilled)
                {
                    medium.Add(new Column(sequence.Item1.Ticks, count: 0));
                }
                
                if (!highFilled)
                {
                    high.Add(new Column(sequence.Item1.Ticks, count: 0));
                }
                
                if (!criticalFilled)
                {
                    critical.Add(new Column(sequence.Item1.Ticks, count: 0));
                }
            }

            return new Timeline(information, low, medium, high, critical);
        }
        
        static string DateFilter(DateTimeOffset start, DateTimeOffset end)
        {
            return $"D.Date >= {start.Ticks} AND D.Date <= {end.Ticks}";
        }

        static IEnumerable<Tuple<DateTimeOffset, DateTimeOffset>> GetSequences(DateTimeOffset start, DateTimeOffset end, int count)
        {
            foreach (var range in SplitDateRange(start, end, count))
            {
                yield return new Tuple<DateTimeOffset, DateTimeOffset>(range.Item1, range.Item2);
            }
        }
    }
}