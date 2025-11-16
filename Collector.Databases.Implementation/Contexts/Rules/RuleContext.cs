using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector;

namespace Collector.Databases.Implementation.Contexts.Rules;

public sealed class RuleContext(ILogger<RuleContext> logger, IHostApplicationLifetime hostApplicationLifetime)
    : CollectorContextBase(logger, hostApplicationLifetime, DbPath, "rules.db")
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
                               PRAGMA auto_vacuum = 0;
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
                               CREATE TABLE IF NOT EXISTS "Rules" (
                                   "RuleId" TEXT NOT NULL CONSTRAINT "PK_Rules" PRIMARY KEY,
                                   "Title" TEXT NOT NULL UNIQUE,
                                   "Author" TEXT NOT NULL,
                                   "Details" TEXT NULL,
                                   "Description" TEXT NULL,
                                   "Date" TEXT NOT NULL,
                                   "Modified" TEXT NULL,
                                   "Level" TEXT NOT NULL,
                                   "Reference" TEXT NULL,
                                   "FalsePositives" TEXT NULL,
                                   "Tags" TEXT NULL,
                                   "Status" TEXT NOT NULL,
                                   "CorrelationOrAggregationTimeSpan" INTEGER NULL,
                                   "DetectionCount" INTEGER NOT NULL DEFAULT 0,
                                   "Updated" INTEGER NULL,
                                   "Builtin" INTEGER NOT NULL DEFAULT 1,
                                   "Enabled" INTEGER NOT NULL DEFAULT 1,
                                   "Content" BLOB NOT NULL,
                                   "GroupName" TEXT NOT NULL DEFAULT 'Uncategorized',
                                   "MitreId" TEXT NULL,
                                   "MitreTactic" TEXT NULL,
                                   "MitreTechnique" TEXT NULL,
                                   "MitreSubTechnique" TEXT NULL,
                                   "Volume" INTEGER NOT NULL DEFAULT 0,
                                   "Source" TEXT NOT NULL
                               );

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