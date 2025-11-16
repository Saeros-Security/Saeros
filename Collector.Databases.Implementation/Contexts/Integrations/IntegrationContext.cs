using System.Text.Json;
using Collector.Databases.Implementation.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector;
using Shared.Integrations;
using Shared.Integrations.ElasticSearch;
using Shared.Integrations.Graylog;
using Shared.Integrations.OpenSearch;
using Shared.Integrations.QRadar;
using Shared.Integrations.Splunk;
using Shared.Integrations.Syslog;
using Shared.Models.Detections;

namespace Collector.Databases.Implementation.Contexts.Integrations;

public sealed class IntegrationContext(ILogger<IntegrationContext> logger, IHostApplicationLifetime hostApplicationLifetime)
    : CollectorContextBase(logger, hostApplicationLifetime, DbPath, "integrations.db")
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;

    public override void CreateTables()
    {
        try
        {
            SetPragmas();
            CreateIntegrationTable();
            var elasticSearchIntegration = new ElasticSearchIntegration(id: 0, IntegrationStatus.Idle, Enum.GetValues<DetectionSeverity>(), enabled: false, ElasticSearchIntegration.CreateSettings());
            var openSearchIntegration = new OpenSearchIntegration(id: 0, IntegrationStatus.Idle, Enum.GetValues<DetectionSeverity>(), enabled: false, OpenSearchIntegration.CreateSettings());
            var splunkIntegration = new SplunkIntegration(id: 0, IntegrationStatus.Idle, Enum.GetValues<DetectionSeverity>(), enabled: false, SplunkIntegration.CreateSettings());
            var syslogIntegration = new SyslogIntegration(id: 0, IntegrationStatus.Idle, Enum.GetValues<DetectionSeverity>(), enabled: false, SyslogIntegration.CreateSettings());
            var graylogIntegration = new GraylogIntegration(id: 0, IntegrationStatus.Idle, Enum.GetValues<DetectionSeverity>(), enabled: false, GraylogIntegration.CreateSettings());
            var qRadarIntegration = new QRadarIntegration(id: 0, IntegrationStatus.Idle, Enum.GetValues<DetectionSeverity>(), enabled: false, QRadarIntegration.CreateSettings());
            var integrations = new List<IntegrationBase>
            {
                elasticSearchIntegration,
                openSearchIntegration,
                splunkIntegration,
                syslogIntegration,
                graylogIntegration,
                qRadarIntegration
            };

            CreateIntegrations(integrations);
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
    
    private void CreateIntegrationTable()
    {
        try
        {
            const string sql = """
                               CREATE TABLE IF NOT EXISTS "Integrations" (
                                   "Id" INTEGER NOT NULL CONSTRAINT "PK_Integrations" PRIMARY KEY,
                                   "Name" TEXT NOT NULL UNIQUE,
                                   "IntegrationType" INTEGER NOT NULL,
                                   "Settings" BLOB NOT NULL,
                                   "Severities" BLOB NOT NULL,
                                   "Enabled" INTEGER NOT NULL DEFAULT 0,
                                   "Status" INTEGER NOT NULL DEFAULT 0
                               )
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
    
    private void CreateIntegrations(IEnumerable<IntegrationBase> integrations)
    {
        try
        {
            using var connection = CreateSingleConnection();
            connection.DbConnection.Open();
            using var transaction = connection.DbConnection.BeginTransaction();
            using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                @"
            INSERT OR IGNORE INTO Integrations (Name, IntegrationType, Settings, Severities)
            VALUES (@Name, @IntegrationType, @Settings, @Severities);
";
            var nameParameter = command.Parameters.Add("Name", SqliteType.Text);
            var integrationTypeParameter = command.Parameters.Add("IntegrationType", SqliteType.Integer);
            var settingsParameter = command.Parameters.Add("Settings", SqliteType.Blob);
            var severitiesParameter = command.Parameters.Add("Severities", SqliteType.Blob);
            foreach (var integration in integrations)
            {
                nameParameter.Value = integration.Name;
                integrationTypeParameter.Value = (int)integration.IntegrationType;
                settingsParameter.Value = EncryptionHelper.Encrypt(JsonSerializer.SerializeToUtf8Bytes(integration.Settings));
                severitiesParameter.Value = JsonSerializer.SerializeToUtf8Bytes(Enum.GetValues<DetectionSeverity>());
                command.ExecuteNonQuery();
            }
            
            transaction.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
            throw;
        }
    }
}