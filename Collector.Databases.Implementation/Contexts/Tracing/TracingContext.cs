using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector;

namespace Collector.Databases.Implementation.Contexts.Tracing;

public sealed class TracingContext(ILogger<TracingContext> logger, IHostApplicationLifetime hostApplicationLifetime)
    : CollectorContextBase(logger, hostApplicationLifetime, DbPath, "tracing.db")
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;

    public override void CreateTables()
    {
        try
        {
            SetPragmas();
            CreateHashTable();
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
    
    private void CreateHashTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "Hashes" (
                                   "Bucket" TEXT NOT NULL,
                                   "Key" TEXT NOT NULL,
                                   "Hash" TEXT NOT NULL CONSTRAINT "PK_Hashes" PRIMARY KEY,
                                   "Date" INTEGER NOT NULL,
                                   "Value" BLOB NOT NULL,
                                   "LogonId" INTEGER NULL,
                                   "UserName" TEXT NULL,
                                   "UserSid" TEXT NULL,
                                   "IpAddressUser" TEXT NULL,
                                   "WorkstationName" TEXT NULL,
                                   "ProcessName" TEXT NULL
                               );

                               CREATE INDEX IF NOT EXISTS idx_hashes_bucket_key_date ON Hashes(Bucket, Key, Date DESC);
                               CREATE INDEX IF NOT EXISTS idx_hashes_process_date ON Hashes(ProcessName, Date DESC) WHERE ProcessName IS NOT NULL;
                               CREATE INDEX IF NOT EXISTS idx_hashes_workstation ON Hashes(WorkstationName) WHERE WorkstationName IS NOT NULL;
                               CREATE INDEX IF NOT EXISTS idx_hashes_logon ON Hashes(LogonId) WHERE LogonId IS NOT NULL;
                               CREATE INDEX IF NOT EXISTS idx_hashes_username ON Hashes(UserName) WHERE UserName IS NOT NULL;
                               CREATE INDEX IF NOT EXISTS idx_hashes_usersid ON Hashes(UserSid) WHERE UserSid IS NOT NULL;
                               CREATE INDEX IF NOT EXISTS idx_hashes_ip ON Hashes(IpAddressUser) WHERE IpAddressUser IS NOT NULL;
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