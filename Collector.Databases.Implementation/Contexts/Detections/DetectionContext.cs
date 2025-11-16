using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector;

namespace Collector.Databases.Implementation.Contexts.Detections;

public sealed class DetectionContext(ILogger<DetectionContext> logger, IHostApplicationLifetime hostApplicationLifetime)
    : CollectorContextBase(logger, hostApplicationLifetime, DbPath, "detections.db")
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
                               CREATE TABLE IF NOT EXISTS "Detections" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Detections" PRIMARY KEY,
                                   "TitleId" INTEGER NOT NULL,
                                   "Duration" INTEGER NOT NULL,
                                   "Date" INTEGER NOT NULL,
                                   "RuleId" INTEGER NOT NULL,
                                   "LevelId" INTEGER NOT NULL,
                                   "ComputerId" INTEGER NOT NULL,
                                   "DetailsId" INTEGER NOT NULL,
                                   "ProviderId" INTEGER NOT NULL,
                                   "ChannelId" INTEGER NOT NULL,
                                   "EventId" INTEGER NOT NULL,
                                   "MitreId" INTEGER NOT NULL
                               );

                               CREATE TABLE IF NOT EXISTS "Computers" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Computers" PRIMARY KEY,
                                   "Value" TEXT NOT NULL UNIQUE
                               );

                               CREATE TABLE IF NOT EXISTS "Levels" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Levels" PRIMARY KEY,
                                   "Value" TEXT NOT NULL UNIQUE
                               );

                               CREATE TABLE IF NOT EXISTS "Rules" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Rules" PRIMARY KEY,
                                   "Value" TEXT NOT NULL UNIQUE,
                                   "Title" TEXT NOT NULL
                               );

                               CREATE TABLE IF NOT EXISTS "EventTitles" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_EventTitles" PRIMARY KEY,
                                   "Value" TEXT NOT NULL UNIQUE
                               );

                               CREATE TABLE IF NOT EXISTS "Titles" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Titles" PRIMARY KEY,
                                   "Value" TEXT NOT NULL UNIQUE
                               );

                               CREATE TABLE IF NOT EXISTS "Mitres" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Mitres" PRIMARY KEY,
                                   "MitreId" TEXT NOT NULL UNIQUE,
                                   "Tactic" TEXT NOT NULL,
                                   "Technique" TEXT NOT NULL,
                                   "SubTechnique" TEXT NOT NULL
                               );

                               CREATE TABLE IF NOT EXISTS "Providers" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Providers" PRIMARY KEY,
                                   "Value" TEXT NOT NULL UNIQUE,
                                   "Guid" TEXT NOT NULL
                               );

                               CREATE TABLE IF NOT EXISTS "Channels" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Channels" PRIMARY KEY,
                                   "Value" TEXT NOT NULL UNIQUE
                               );

                               CREATE TABLE IF NOT EXISTS "Details" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Details" PRIMARY KEY,
                                   "Hash" TEXT NOT NULL UNIQUE,
                                   "Value" BLOB NOT NULL
                               );

                               CREATE TABLE IF NOT EXISTS "Exclusions" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Exclusions" PRIMARY KEY,
                                   "Value" BLOB NOT NULL,
                                   "Hash" TEXT NOT NULL UNIQUE,
                                   "Count" INTEGER NOT NULL DEFAULT 0,
                                   "Activity" INTEGER NOT NULL
                               );

                               CREATE INDEX IF NOT EXISTS idx_detections_date_id ON Detections (Date, Id);
                               CREATE INDEX IF NOT EXISTS idx_detections_computer_level_rule_mitre_date ON Detections(ComputerId, LevelId, RuleId, MitreId, Date);
                               CREATE INDEX IF NOT EXISTS idx_mitres_tactic_technique_subtechnique ON Mitres(Tactic, Technique, SubTechnique);
                               CREATE INDEX IF NOT EXISTS idx_rules_title ON Rules(Title);
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