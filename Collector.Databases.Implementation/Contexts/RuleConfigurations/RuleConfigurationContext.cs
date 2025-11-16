using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector;

namespace Collector.Databases.Implementation.Contexts.RuleConfigurations;

public sealed class RuleConfigurationContext(ILogger<RuleConfigurationContext> logger, IHostApplicationLifetime hostApplicationLifetime)
    : CollectorContextBase(logger, hostApplicationLifetime, DbPath, "rule-configurations.db")
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;

    public override void CreateTables()
    {
        try
        {
            SetPragmas();
            CreateTable();
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

            using var connection = CreateSingleConnection();
            connection.DbConnection.Open();
            using var command = connection.DbConnection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
            throw;
        }
    }

    private void CreateTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "RuleConfigurations" (
                                   "Type" INTEGER NOT NULL,
                                   "Content" TEXT NOT NULL,
                                   PRIMARY KEY (Type, Content)
                               );

                               CREATE INDEX IF NOT EXISTS idx_ruleconfigurations_type ON RuleConfigurations (Type);

                               """;

            using var connection = CreateSingleConnection();
            connection.DbConnection.Open();
            using var command = connection.DbConnection.CreateCommand();
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