using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Collector.Databases.Implementation.Helpers;

#pragma warning disable 1591

public class SqliteRowUpdatingEventArgs(
    DataRow dataRow,
    IDbCommand? command,
    StatementType statementType,
    DataTableMapping tableMapping)
    : RowUpdatingEventArgs(dataRow, command, statementType, tableMapping);

public class SqliteRowUpdatedEventArgs(
    DataRow dataRow,
    IDbCommand? command,
    StatementType statementType,
    DataTableMapping tableMapping)
    : RowUpdatedEventArgs(dataRow, command, statementType, tableMapping);

#pragma warning restore 1591

public delegate void SqliteRowUpdatedEventHandler(object sender, SqliteRowUpdatedEventArgs e);
public delegate void SqliteRowUpdatingEventHandler(object sender, SqliteRowUpdatingEventArgs e);

public sealed class SqliteDataAdapter : DbDataAdapter
{
    public event SqliteRowUpdatedEventHandler? RowUpdated;
    public event SqliteRowUpdatingEventHandler? RowUpdating;

    public SqliteDataAdapter(SqliteCommand selectCommand)
    {
        SelectCommand = selectCommand;
    }

    protected override RowUpdatedEventArgs CreateRowUpdatedEvent(DataRow dataRow, IDbCommand? command,
        StatementType statementType,
        DataTableMapping tableMapping)
    {
        return new SqliteRowUpdatedEventArgs(dataRow, command, statementType, tableMapping);
    }

    protected override RowUpdatingEventArgs CreateRowUpdatingEvent(DataRow dataRow, IDbCommand? command,
        StatementType statementType,
        DataTableMapping tableMapping)
    {
        return new SqliteRowUpdatingEventArgs(dataRow, command, statementType, tableMapping);
    }

    protected override void OnRowUpdated(RowUpdatedEventArgs value)
    {
        if (RowUpdated != null && value is SqliteRowUpdatedEventArgs args)
            RowUpdated(this, args);
    }

    protected override void OnRowUpdating(RowUpdatingEventArgs value)
    {
        if (RowUpdating != null && value is SqliteRowUpdatingEventArgs args)
            RowUpdating(this, args);
    }

    public new SqliteCommand? DeleteCommand
    {
        get => (SqliteCommand?)base.DeleteCommand;
        set => base.DeleteCommand = value;
    }

    public new SqliteCommand? SelectCommand
    {
        get => (SqliteCommand?)base.SelectCommand;
        set => base.SelectCommand = value;
    }

    public new SqliteCommand? UpdateCommand
    {
        get => (SqliteCommand?)base.UpdateCommand;
        set => base.UpdateCommand = value;
    }

    public new SqliteCommand? InsertCommand
    {
        get => (SqliteCommand?)base.InsertCommand;
        set => base.InsertCommand = value;
    }
}