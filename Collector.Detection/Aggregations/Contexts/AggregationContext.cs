using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector;

namespace Collector.Detection.Aggregations.Contexts;

internal sealed class AggregationContext(ILogger logger, IHostApplicationLifetime hostApplicationLifetime, string ruleId, string path)
    : CollectorContextBase(logger, hostApplicationLifetime, Path.Join(path, "Aggregations"), $"{ruleId}.db")
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;

    public override void CreateTables()
    {
        try
        {
            SetPragmas();
            DropTableIfExists();
            CreateTable();
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Could not create database tables");
            _hostApplicationLifetime.StopApplication();
        }
    }

    private void DropTableIfExists()
    {
        using var connection = CreateSingleConnection();
        connection.DbConnection.Open();
        using var command = connection.DbConnection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS Aggregations;";
        command.ExecuteNonQuery();
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
            Logger.LogError(ex, "An error has occurred");
            throw;
        }
    }

    private void CreateTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "Aggregations" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Aggregations" PRIMARY KEY,
                                   "EventID" TEXT NOT NULL,
                                   "Channel" TEXT NOT NULL,
                                   "SystemTime" TEXT NOT NULL,
                                   "Name" TEXT NOT NULL,
                                   "Guid" TEXT NOT NULL
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
            Logger.LogError(ex, "An error has occurred");
            throw;
        }
    }
}