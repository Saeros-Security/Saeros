using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using Collector.Databases.Abstractions.Repositories.Integrations;
using Collector.Databases.Implementation.Contexts.Integrations;
using Collector.Databases.Implementation.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Shared.Integrations;
using Shared.Integrations.ElasticSearch;
using Shared.Integrations.OpenSearch;
using Shared.Models.Console.Requests;
using Shared.Models.Detections;

namespace Collector.Databases.Implementation.Repositories.Integrations;

public sealed class IntegrationRepository(IntegrationContext integrationContext) : IIntegrationRepository
{
    private readonly Subject<Unit> _integrationChangedSubject = new();

    public async Task<IEnumerable<IntegrationBase>> GetIntegrationsAsync(CancellationToken cancellationToken)
    {
        var integrations = new List<IntegrationBase>();
        await using var connection = integrationContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, IntegrationType, Settings, Severities, Enabled, Status FROM Integrations;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var type = (IntegrationType)reader.GetInt32(2);
            var settings = JsonSerializer.Deserialize<IDictionary<string, string>>(Encoding.UTF8.GetString(EncryptionHelper.Decrypt(reader.GetFieldValue<byte[]>(3))))!;
            var severities = JsonSerializer.Deserialize<IEnumerable<DetectionSeverity>>(Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(4)))!;
            var enabled = reader.GetBoolean(5);
            var status = (IntegrationStatus)reader.GetInt32(6);
            switch (type)
            {
                case IntegrationType.OpenSearch:
                    integrations.Add(new OpenSearchIntegration(id, name, status, type, severities, enabled, settings));
                    break;
                case IntegrationType.ElasticSearch:
                    integrations.Add(new ElasticSearchIntegration(id, name, status, type, severities, enabled, settings));
                    break;
                /*
                case IntegrationType.Splunk:
                    integrations.Add(new SplunkIntegration(id, name, status, type, severities, enabled, settings));
                    break;
                case IntegrationType.Syslog:
                    integrations.Add(new SyslogIntegration(id, name, status, type, severities, enabled, settings));
                    break;
                case IntegrationType.Graylog:
                    integrations.Add(new GraylogIntegration(id, name, status, type, severities, enabled, settings));
                    break;
                case IntegrationType.QRadar:
                    integrations.Add(new QRadarIntegration(id, name, status, type, severities, enabled, settings));
                    break;
                */
            }
        }

        return integrations;
    }
    
    public async Task UpdateIntegrationAsync(UpdateIntegration updateIntegration, CancellationToken cancellationToken)
    {
        using var connection = await integrationContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var command = connection.DbConnection.CreateCommand();
        if (updateIntegration.Enabled)
        {
            command.CommandText =
                @"
            UPDATE Integrations
            SET Settings = @Settings, Enabled = @Enabled, Severities = @Severities
            WHERE Id = @Id;
";
        }
        else
        {
            command.CommandText =
                @"
            UPDATE Integrations
            SET Settings = @Settings, Enabled = @Enabled, Severities = @Severities, Status = 0
            WHERE Id = @Id;
";
        }

        command.Parameters.Add(new SqliteParameter("Id", DatabaseHelper.GetValue(updateIntegration.Id)));
        command.Parameters.Add(new SqliteParameter("Enabled", DatabaseHelper.GetValue(updateIntegration.Enabled)));
        command.Parameters.Add(new SqliteParameter("Settings", DatabaseHelper.GetValue(EncryptionHelper.Encrypt(JsonSerializer.SerializeToUtf8Bytes(updateIntegration.Settings)))));
        command.Parameters.Add(new SqliteParameter("Severities", DatabaseHelper.GetValue(JsonSerializer.SerializeToUtf8Bytes(updateIntegration.Severities))));
        await command.ExecuteNonQueryAsync(cancellationToken);
        _integrationChangedSubject.OnNext(Unit.Default);
    }

    public async Task SetStatusAsync(int id, IntegrationStatus status, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await integrationContext.CreateConnectionAsync(cancellationToken);
            await connection.DbConnection.OpenAsync(cancellationToken);
            await using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                @"
            UPDATE Integrations
            SET Status = @Status
            WHERE Id = @Id;
";
            command.Parameters.Add(new SqliteParameter("Id", DatabaseHelper.GetValue(id)));
            command.Parameters.Add(new SqliteParameter("Status", DatabaseHelper.GetValue((int)status)));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            integrationContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            integrationContext.Logger.LogError(ex, "An error has occurred");
        }
    }

    public void OnIntegrationChanged()
    {
        _integrationChangedSubject.OnNext(Unit.Default);
    }

    public IObservable<Unit> IntegrationChanged => _integrationChangedSubject;

    public void Dispose()
    {
        _integrationChangedSubject.Dispose();
    }
}