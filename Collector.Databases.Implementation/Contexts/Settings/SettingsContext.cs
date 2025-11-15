using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector;
using Shared.Models.Console.Responses;

namespace Collector.Databases.Implementation.Contexts.Settings;

public sealed class SettingsContext(ILogger<SettingsContext> logger, IHostApplicationLifetime hostApplicationLifetime)
    : CollectorContextBase(logger, hostApplicationLifetime, DbPath, "settings.db")
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;

    public override void CreateTables()
    {
        try
        {
            SetPragmas();
            CreateSettingsTable();
            CreateDomainTable();
            CreateComputerTable();
            InsertProfile(name: "Default");
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

    private void CreateSettingsTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "Settings" (
                                   "Name" TEXT NOT NULL CONSTRAINT "PK_Settings" PRIMARY KEY,
                                   "Profile" INTEGER NOT NULL DEFAULT 0,
                                   "Retention" INTEGER NOT NULL DEFAULT 12096000000000,
                                   "LockoutThreshold" INTEGER NOT NULL DEFAULT 15,
                                   "OverrideAuditPolicies" INTEGER NOT NULL DEFAULT 1
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
    
    private void CreateComputerTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "Computers" (
                                   "Computer" TEXT NOT NULL CONSTRAINT "PK_Computers" PRIMARY KEY,
                                   "Domain" TEXT NULL,
                                   "IpAddress" TEXT NOT NULL,
                                   "OperatingSystem" TEXT NOT NULL,
                                   "UpTime" INTEGER NOT NULL,
                                   "MedianCpuUsage" INTEGER NOT NULL,
                                   "MedianWorkingSet" INTEGER NOT NULL,
                                   "Version" TEXT NOT NULL
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
    
    private void CreateDomainTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "Domains" (
                                   "Name" TEXT NOT NULL CONSTRAINT "PK_Domains" PRIMARY KEY,
                                   "PrimaryDomainController" TEXT NOT NULL,
                                   "DomainControllerCount" INTEGER NOT NULL,
                                   "ShouldUpdate" INTEGER NOT NULL
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
    
    private void InsertProfile(string name)
    {
        try
        {
            using var connection = CreateSingleConnection();
            connection.DbConnection.Open();
            using var transaction = connection.DbConnection.BeginTransaction();
            using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                @"
            INSERT OR IGNORE INTO Settings (Name, Profile)
            VALUES (@Name, @Profile);
";
            var nameParameter = command.Parameters.Add("Name", SqliteType.Text);
            nameParameter.Value = name;
            var profileParameter = command.Parameters.Add("Profile", SqliteType.Integer);
            profileParameter.Value = DetectionProfile.Core;
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
            throw;
        }
    }
}