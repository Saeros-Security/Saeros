using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector;

namespace Collector.Databases.Implementation.Contexts.Dashboards;

public sealed class DashboardContext(ILogger<DashboardContext> logger, IHostApplicationLifetime hostApplicationLifetime)
    : CollectorContextBase(logger, hostApplicationLifetime, DbPath, "dashboards.db")
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;

    public override void CreateTables()
    {
        try
        {
            SetPragmas();
            CreateHomeDashboardsTable();
            CreateRuleDashboardsTable();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Could not create database tables");
            _hostApplicationLifetime.StopApplication();
        }
    }

    private void SetPragmas()
    {
        try
        {
            const string sql = """
                               PRAGMA journal_mode = 'wal';
                               PRAGMA synchronous = normal;
                               PRAGMA auto_vacuum = full;
                               """;

            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
            throw;
        }
    }

    private void CreateHomeDashboardsTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "HomeDashboards" (
                                   "Ticks" INTEGER NOT NULL CONSTRAINT "PK_HomeDashboards" PRIMARY KEY,
                                   "Data" BLOB NOT NULL
                               );

                               CREATE INDEX IF NOT EXISTS idx_home_dashboards_ticks ON HomeDashboards (Ticks);

                               """;

            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
            throw;
        }
    }

    private void CreateRuleDashboardsTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "RuleDashboards" (
                                   "RuleId" TEXT NOT NULL,
                                   "Ticks" INTEGER NOT NULL,
                                   "Data" BLOB NOT NULL,
                                   PRIMARY KEY (RuleId, Ticks)
                               );

                               CREATE INDEX IF NOT EXISTS idx_rule_dashboards_ticks ON RuleDashboards (Ticks);

                               """;

            using var connection = CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
            throw;
        }
    }
}