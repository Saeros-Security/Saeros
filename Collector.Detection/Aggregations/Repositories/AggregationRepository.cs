using System.Collections.Concurrent;
using Collector.Detection.Aggregations.Contexts;
using Collector.Detection.Aggregations.Extensions;
using Collector.Detection.Aggregations.Helpers;
using Collector.Detection.Aggregations.Interfaces;
using Collector.Detection.Events.Details;
using Collector.Detection.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Extensions;

namespace Collector.Detection.Aggregations.Repositories;

public sealed class AggregationRepository(ILogger<AggregationRepository> logger, IHostApplicationLifetime hostApplicationLifetime, string dbPath) : IAggregationRepository
{
    private static readonly ISet<string> InternalColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Id",
        "Count"
    };
    
    private readonly struct ProviderGuidAndEventId(Guid providerGuid, ushort eventId) : IEquatable<ProviderGuidAndEventId>
    {
        private Guid ProviderGuid { get; } = providerGuid;
        private ushort EventId { get; } = eventId;

        public bool Equals(ProviderGuidAndEventId other)
        {
            return ProviderGuid.Equals(other.ProviderGuid) && EventId == other.EventId;
        }

        public override bool Equals(object? obj)
        {
            return obj is ProviderGuidAndEventId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ProviderGuid, EventId);
        }
    }
    
    private readonly ConcurrentDictionary<string, Lazy<AggregationContext>> _contexts = new(StringComparer.OrdinalIgnoreCase);

    private AggregationContext GetContext(string ruleId)
    {
        return _contexts.GetOrAdd(ruleId, valueFactory: id =>
        {
            return new Lazy<AggregationContext>(() =>
            {
                var context = new AggregationContext(logger, hostApplicationLifetime, id, path: dbPath);
                context.CreateTables();
                return context;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }).Value;
    }
    
    public async Task InsertAsync(IDictionary<AggregationRule, IEnumerable<WinEvent>> aggregations, Action<AggregationRule, long, ISet<string>> onInsert, IProvideRuleProperties rulePropertiesProvider, CancellationToken cancellationToken)
    {
        await Task.WhenAll(aggregations.Select(async aggregation =>
        {
            var context = GetContext(aggregation.Key.Id);
            try
            {
                await using var connection = context.CreateConnection();
                await connection.OpenAsync(cancellationToken);

                var providerEventIds = aggregation.Value.Select(winEvent => new ProviderEventId(winEvent.System[WinEventExtensions.ProviderNameKey], winEvent.System[WinEventExtensions.EventIdKey])).ToHashSet();
                var ruleProperties = rulePropertiesProvider.GetProperties(aggregation.Key.Id).Concat(aggregation.Key.AggregationProperties).Concat(DetectionDetailsResolver.GetProperties(aggregation.Key.Metadata, providerEventIds)).ExtractProperties().ToHashSet(StringComparer.OrdinalIgnoreCase);
                var columns = await connection.CreateColumnsAsync(logger, ColumnHelper.ExtractColumns(aggregation.Value, ruleProperties), cancellationToken);
                await connection.CreateIndexAsync(logger, indexes: aggregation.Key.AggregationProperties.Intersect(columns).ToHashSet(), cancellationToken);

                var groups = aggregation.Value.GroupBy(winEvent => new ProviderGuidAndEventId(winEvent.ProviderGuid, winEvent.EventId));
                await InsertAsync(connection, aggregation.Key, groups, onInsert, ruleProperties, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                context.Logger.LogWarning("Cancellation has occurred");
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "An error has occurred");
            }
        }));
    }

    private static async Task InsertAsync(SqliteConnection connection, AggregationRule rule, IEnumerable<IGrouping<ProviderGuidAndEventId, WinEvent>> groups, Action<AggregationRule, long, ISet<string>> onInsert, HashSet<string> ruleProperties, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        foreach (var group in groups)
        {
            var parameters = new Dictionary<string, SqliteParameter>(StringComparer.OrdinalIgnoreCase);
            var columnNames = ColumnHelper.ExtractColumns(group.First(), ruleProperties).Select(column => column.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (columnNames.Count == 0)
            {
                continue;
            }

            if (!columnNames.Except(WinEventExtensions.SystemColumns, StringComparer.OrdinalIgnoreCase).Any())
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                        INSERT INTO Aggregations ({string.Join(",", columnNames.Select(column => column))})
                        VALUES ({string.Join(",", columnNames.Select(column => $"@{column}"))});

                        SELECT last_insert_rowid();
                        ";
            
            foreach (var winEventColumns in group.Select(winEvent => ColumnHelper.ExtractColumns(winEvent, ruleProperties)))
            {
                foreach (var column in winEventColumns)
                {
                    if (parameters.TryGetValue(column.Key, out var parameter))
                    {
                        parameter.Value = column.Value;
                    }
                    else
                    {
                        parameters.Add(column.Key, command.Parameters.Add(new SqliteParameter(column.Key, column.Value)));
                    }
                }

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    onInsert(rule, reader.GetInt64(ordinal: 0), columnNames);
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }
    
    private static IEnumerable<WinEvent> Query(SqliteDataReader reader)
    { 
        var columns = Enumerable.Range(0, reader.FieldCount).Select(i => new Tuple<int, string>(i, reader.GetName(i))).ToDictionary(kvp => kvp.Item1, kvp => kvp.Item2);
        while (reader.Read())
        {
            var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Count; i++)
            {
                if (reader.IsDBNull(i))
                {
                    continue;
                }
                
                var columnName = columns[i];
                if (InternalColumns.Contains(columnName)) continue;
                if (WinEventExtensions.SystemColumns.Contains(columnName))
                {
                    var column = reader.GetString(i);
                    system.Add(columnName, column);
                }
                else
                {
                    var column = reader.GetString(i);
                    eventData.Add(columnName, column);
                }
            }

            yield return new WinEvent(system, eventData);
        }
    }
    
    public IEnumerable<WinEvent> Query(string ruleId, string query)
    {
        var context = GetContext(ruleId);
        using var connection = context.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = query;
        using var reader = command.ExecuteReader();
        foreach (var winEvents in Query(reader))
        {
            yield return winEvents;
        }
    }

    public async Task DeleteAsync(string ruleId, ISet<long> ids, CancellationToken cancellationToken)
    {
        var context = GetContext(ruleId);
        try
        {
            await using var connection = context.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM Aggregations WHERE Id = @Id;";
            var idParameter = deleteCommand.Parameters.Add("Id", SqliteType.Integer);
            foreach (var id in ids)
            {
                idParameter.Value = id;
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            context.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "An error has occurred");
        }
    }
}