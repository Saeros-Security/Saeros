using System.Data;
using System.IO.Compression;
using Collector.Databases.Abstractions.Repositories.Dashboards;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Integrations;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Implementation.Contexts.Detections;
using Collector.Databases.Implementation.Contexts.Integrations;
using Collector.Databases.Implementation.Contexts.Rules;
using Collector.Databases.Implementation.Contexts.Settings;
using Collector.Databases.Implementation.Helpers;
using Collector.Services.Abstractions.Databases;
using Collector.Services.Abstractions.Rules;
using Microsoft.Data.Sqlite;
using Shared.Databases.Collector;
using Shared.Models.Console.Requests;

namespace Collector.Services.Implementation.Bridge.Databases;

public sealed class DatabaseExporterService(
    RuleContext ruleContext,
    DetectionContext detectionContext,
    IntegrationContext integrationContext,
    SettingsContext settingsContext,
    IRuleRepository ruleRepository,
    IDetectionRepository detectionRepository,
    IIntegrationRepository integrationRepository,
    ISettingsRepository settingsRepository,
    IDashboardRepository dashboardRepository,
    IRuleService ruleService) : IDatabaseExporterService
{
    public async Task ExportTablesAsync(string path, CancellationToken cancellationToken)
    {
        await using var zipToOpen = new FileStream(path, FileMode.Create);
        await using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create);

        var rulesEntry = archive.CreateEntry("Rules.bak");
        await using (var writer = new StreamWriter(await rulesEntry.OpenAsync(cancellationToken)))
        {
            await ExportTableCoreAsync(writer, ruleContext, tableName: "Rules", cancellationToken);
        }

        var exclusionsEntry = archive.CreateEntry("Exclusions.bak");
        await using (var writer = new StreamWriter(await exclusionsEntry.OpenAsync(cancellationToken)))
        {
            await ExportTableCoreAsync(writer, detectionContext, tableName: "Exclusions", cancellationToken);
        }

        var integrationsEntry = archive.CreateEntry("Integrations.bak");
        await using (var writer = new StreamWriter(await integrationsEntry.OpenAsync(cancellationToken)))
        {
            await ExportTableCoreAsync(writer, integrationContext, tableName: "Integrations", cancellationToken);
        }

        var settingsEntry = archive.CreateEntry("Settings.bak");
        await using (var writer = new StreamWriter(await settingsEntry.OpenAsync(cancellationToken)))
        {
            await ExportTableCoreAsync(writer, settingsContext, tableName: "Settings", cancellationToken);
        }
    }

    private static async Task ExportTableCoreAsync(StreamWriter writer, CollectorContextBase context, string tableName, CancellationToken cancellationToken)
    {
        using var connection = await context.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText = $"SELECT * FROM {tableName};";
        var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (reader.HasRows)
        {
            using var dataTable = new DataTable();
            dataTable.Load(reader);

            dataTable.WriteXml(writer, XmlWriteMode.WriteSchema);
            await writer.FlushAsync(cancellationToken);
        }
    }

    public async Task ImportTablesAsync(string path, CancellationToken cancellationToken)
    {
        await using var archive = await ZipFile.OpenReadAsync(path, cancellationToken);
        foreach (var entry in archive.Entries)
        {
            await using var stream = await entry.OpenAsync(cancellationToken);
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Seek(0, SeekOrigin.Begin);
            if (ms.Length == 0) continue;

            using var dataTable = new DataTable();
            dataTable.ReadXml(ms);

            if (dataTable.TableName.Equals("Rules"))
            {
                var rules = await ruleRepository.GetAsync(cancellationToken);
                await detectionRepository.DeleteRulesAsync(rules.Items.Select(contract => new DeleteRule(contract.Id)).ToList(), cancellationToken);
                await detectionRepository.ComputeMetricsAsync(cancellationToken);
                
                // Send rule disablement towards DCs
                await ruleService.DisableRulesAsync(rules.Items.Select(contract => new DisableRule(contract.Id)).ToList(), commit: false, cancellationToken);

                using var connection = await ruleContext.CreateConnectionAsync(cancellationToken);
                await connection.DbConnection.OpenAsync(cancellationToken);
                await ImportTableCoreAsync(dataTable, connection.DbConnection, cancellationToken);
            }
            else if (dataTable.TableName.Equals("Exclusions"))
            {
                using var connection = await detectionContext.CreateConnectionAsync(cancellationToken);
                await connection.DbConnection.OpenAsync(cancellationToken);
                await ImportTableCoreAsync(dataTable, connection.DbConnection, cancellationToken);
            }
            else if (dataTable.TableName.Equals("Integrations"))
            {
                using var connection = await integrationContext.CreateConnectionAsync(cancellationToken);
                await connection.DbConnection.OpenAsync(cancellationToken);
                await ImportTableCoreAsync(dataTable, connection.DbConnection, cancellationToken);
            }
            else if (dataTable.TableName.Equals("Settings"))
            {
                using var connection = await settingsContext.CreateConnectionAsync(cancellationToken);
                await connection.DbConnection.OpenAsync(cancellationToken);
                await ImportTableCoreAsync(dataTable, connection.DbConnection, cancellationToken);
            }
        }

        integrationRepository.OnIntegrationChanged();
        await ruleRepository.InitializeAsync(cancellationToken);
        await detectionRepository.InitializeAsync(cancellationToken);
        await settingsRepository.InitializeAsync(cancellationToken);
        await ruleService.UpdateAsync(includeNonBuiltinRules: true, cancellationToken);
        await dashboardRepository.DeleteDashboards(cancellationToken);
    }

    private static async Task ImportTableCoreAsync(DataTable dataTable, SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = $"DELETE FROM {dataTable.TableName};";
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {dataTable.TableName};";
        using var adapter = new SqliteDataAdapter(command);
        using var commandBuilder = new SqliteCommandBuilder(adapter);
        commandBuilder.Update(dataTable);
        await transaction.CommitAsync(cancellationToken);
    }
}