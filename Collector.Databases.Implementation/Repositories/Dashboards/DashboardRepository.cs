using System.Text.Json;
using App.Metrics;
using Collector.Core;
using Collector.Core.Extensions;
using Collector.Databases.Abstractions.Repositories.Dashboards;
using Collector.Databases.Implementation.Contexts.Dashboards;
using Collector.Databases.Implementation.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Shared.Models.Console.Responses;
using Shared.Serialization;

namespace Collector.Databases.Implementation.Repositories.Dashboards;

public sealed class DashboardRepository(DashboardContext dashboardContext, IMetricsRoot metrics)
    : IDashboardRepository
{
    public async Task StoreHomeMetricsAsync(byte[] serializedMetrics, CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.Today.Date;
            using var connection = await dashboardContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                @"
                INSERT OR REPLACE INTO HomeDashboards (Ticks, Data)
                VALUES (@Ticks, @Data);
";

            var ticks = command.Parameters.Add("Ticks", SqliteType.Integer);
            var data = command.Parameters.Add("Data", SqliteType.Blob);
            ticks.Value = DatabaseHelper.GetValue(today.Ticks);
            data.Value = DatabaseHelper.GetValue(serializedMetrics);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dashboardContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            dashboardContext.Logger.LogError(ex, "An error has occurred");
        }
    }

    public async Task StoreRuleMetrics(IDictionary<string, long> ruleCountById, CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.Today.Date;
            var detectionDurationContext = metrics.Snapshot.GetForContext(MetricOptions.DetectionDurations.Context);
            using var connection = await dashboardContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var transaction = connection.DbConnection.BeginTransaction();
            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                @"
                INSERT OR REPLACE INTO RuleDashboards (RuleId, Ticks, Data)
                VALUES (@RuleId, @Ticks, @Data);
";
            var ruleIdParameter = command.Parameters.Add("RuleId", SqliteType.Text);
            var ticks = command.Parameters.Add("Ticks", SqliteType.Integer);
            var data = command.Parameters.Add("Data", SqliteType.Blob);
            foreach (var kvp in ruleCountById)
            {
                var histograms = detectionDurationContext.Histograms.Where(h => h.Name.Contains(MetricOptions.DetectionDurations.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                var histogram = histograms.SingleOrDefault(r => r.Tags.Values.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase));

                var p50 = histogram?.Value.Median ?? 0d;
                var p75 = histogram?.Value.Percentile75 ?? 0d;
                var p95 = histogram?.Value.Percentile95 ?? 0d;
                var p99 = histogram?.Value.Percentile99 ?? 0d;
                var ruleMetrics = new RuleMetrics(kvp.Value, p50, p75, p95, p99, today);

                ruleIdParameter.Value = DatabaseHelper.GetValue(kvp.Key);
                ticks.Value = DatabaseHelper.GetValue(today.Ticks);
                data.Value = DatabaseHelper.GetValue(JsonSerializer.Serialize(ruleMetrics, SerializationContext.Default.RuleMetrics).LZ4CompressString());
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            
            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dashboardContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            dashboardContext.Logger.LogError(ex, "An error has occurred");
        }
    }

    public async Task<IEnumerable<HomeMetrics>> GetHomeMetricsAsync(CancellationToken cancellationToken)
    {
        var homeMetrics = new List<HomeMetrics>();
        try
        {
            await using var connection = dashboardContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                @"
            SELECT Data
            FROM HomeDashboards;
";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var data = reader.GetFieldValue<byte[]>(0);
                homeMetrics.Add(JsonSerializer.Deserialize(data.LZ4UncompressString(), SerializationContext.Default.HomeMetrics) ?? EmptyHomeMetrics());
            }
        }
        catch (OperationCanceledException)
        {
            dashboardContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            dashboardContext.Logger.LogError(ex, "An error has occurred");
        }

        return homeMetrics;
    }

    public async Task<IEnumerable<RuleMetrics>> GetRuleMetrics(string ruleId, CancellationToken cancellationToken)
    {
        var ruleMetrics = new List<RuleMetrics>();
        try
        {
            await using var connection = dashboardContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText =
                @"
            SELECT Data
            FROM RuleDashboards
            WHERE RuleId = @RuleId;
";

            var ruleIdParameter = command.Parameters.Add("RuleId", SqliteType.Text);
            ruleIdParameter.Value = ruleId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var data = reader.GetFieldValue<byte[]>(0);
                ruleMetrics.Add(JsonSerializer.Deserialize(data.LZ4UncompressString(), SerializationContext.Default.RuleMetrics) ?? new RuleMetrics(default, default, default, default, default, DateTime.Today));
            }
        }
        catch (OperationCanceledException)
        {
            dashboardContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            dashboardContext.Logger.LogError(ex, "An error has occurred");
        }

        return ruleMetrics;
    }

    public async Task ApplyRetentionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await dashboardContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                $@"
                DELETE FROM HomeDashboards WHERE datetime(Ticks/10000000 - 62135596800, 'unixepoch') < DATE('now', '-{Constants.MaxRetentionDays} days');
                DELETE FROM RuleDashboards WHERE datetime(Ticks/10000000 - 62135596800, 'unixepoch') < DATE('now', '-{Constants.MaxRetentionDays} days');
";

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dashboardContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            dashboardContext.Logger.LogError(ex, "An error has occurred");
        }
    }
    
    public async Task DeleteDashboards(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await dashboardContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                @"
                DELETE FROM HomeDashboards;
                DELETE FROM RuleDashboards;
";

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            dashboardContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            dashboardContext.Logger.LogError(ex, "An error has occurred");
        }
    }

    public static HomeMetrics EmptyHomeMetrics()
    {
        return new HomeMetrics(new BannerMetric(default, Array.Empty<double>(), default, default),
            new BannerMetric(default, Array.Empty<double>(), default, default),
            new BannerMetric(default, Array.Empty<double>(), default, default),
            new DetectionsSatellite(default, default, default, default, default, default, default, Array.Empty<int>(), Array.Empty<int>(), default, default),
            new SeveritySatellite(default, default, default, default, default, Array.Empty<int[]>(), default),
            new MitreSatellite(new TacticMetric(Array.Empty<HeatmapPointMetric>(), Array.Empty<string>(), Array.Empty<string>())),
            new RulesSatellite(new Dictionary<string, long>(), new Dictionary<string, long>(), new Dictionary<string, long>(), new Dictionary<string, long>(), new Dictionary<string, long>()),
            new EventsSatellite(new SortedDictionary<int, long>(), default, default, default, default),
            new TrafficSatellite(new Dictionary<string, long>(), Array.Empty<OutboundEntry>()),
            default);
    }
}