using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Collector.Detection.Aggregations.Extensions;

internal static class SqlConnectionExtensions
{
    private const string TableInfo = "pragma table_info('Aggregations')";
    private const string IndexInfo = "pragma index_list('Aggregations')";

    private static async Task AddColumnAsync(string name, SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = $"""
                   ALTER TABLE Aggregations ADD COLUMN {name} TEXT;
                   """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    private static async Task AddIndexAsync(string name, SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = $"""
                   CREATE INDEX IF NOT EXISTS idx_aggregations_{name.ToLower()} ON Aggregations ({name});
                   """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<ISet<string>> CreateColumnsAsync(this SqliteConnection connection, ILogger logger, ISet<string> columns, CancellationToken cancellationToken)
    {
        try
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = TableInfo;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var name = reader.GetString(1);
                    if (columns.Contains(name))
                    {
                        existingColumns.Add(name);
                    }
                }
            }

            columns.ExceptWith(existingColumns);
            if (columns.Count == 0) return columns;
            await using var transaction = connection.BeginTransaction();
            foreach (var column in columns)
            {
                await AddColumnAsync(column, connection, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (!ex.Message.Contains("duplicate column name"))
        {
            logger.LogError(ex, "An error has occurred");
        }
        
        return columns;
    }
    
    public static async Task CreateIndexAsync(this SqliteConnection connection, ILogger logger, ISet<string> indexes, CancellationToken cancellationToken)
    {
        try
        {
            var existingIndex = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = IndexInfo;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var name = reader.GetString(1).Split('_', StringSplitOptions.RemoveEmptyEntries).Last();
                    if (indexes.Contains(name))
                    {
                        existingIndex.Add(name);
                    }
                }
            }

            indexes.ExceptWith(existingIndex);
            if (indexes.Count == 0) return;
            await using (var transaction = connection.BeginTransaction())
            {
                foreach (var index in indexes)
                {
                    await AddIndexAsync(index, connection, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA optimize;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (!ex.Message.Contains("duplicate column name"))
        {
            logger.LogError(ex, "An error has occurred");
        }
    }
}