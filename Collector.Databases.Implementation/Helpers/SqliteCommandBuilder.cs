using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Collector.Databases.Implementation.Helpers;

public sealed class SqliteCommandBuilder : DbCommandBuilder
{
    private readonly SqliteDataAdapter _dataAdapter;
    private const string Clause1 = "({0} = 1 AND {1} IS NULL)";
    private const string Clause2 = "({0} = {1})";

    private DataTable? _schemaTable;
    private SqliteCommand? _insertCommand;
    private SqliteCommand? _updateCommand;
    private SqliteCommand? _deleteCommand;
    private bool _disposed;
    private string _quotePrefix = "'";
    private string _quoteSuffix = "'";
    private string? _tableName;
    private SqliteRowUpdatingEventHandler? _rowUpdatingHandler;

    public SqliteCommandBuilder(SqliteDataAdapter adapter)
    {
        _dataAdapter = adapter;
        _dataAdapter.RowUpdating += RowUpdatingHandler;

        adapter.InsertCommand = GetInsertCommand();
        adapter.UpdateCommand = GetUpdateCommand();
        adapter.DeleteCommand = GetDeleteCommand();
    }

    public void Update(DataTable dataTable)
    {
        _dataAdapter.Update(dataTable);
    }
    
    [DefaultValue("")]
    [AllowNull]
    public override string QuotePrefix
    {
        get => _quotePrefix;
        set
        {
            if (_schemaTable != null)
                throw new InvalidOperationException("The QuotePrefix and QuoteSuffix properties cannot be changed once an Insert, Update or Delete commands have been generated.");
            _quotePrefix = value!;
        }
    }

    [DefaultValue("")]
    [AllowNull]
    public override string QuoteSuffix
    {
        get => _quoteSuffix;
        set
        {
            if (_schemaTable != null)
                throw new InvalidOperationException("The QuotePrefix and QuoteSuffix properties cannot be changed once an Insert, Update or Delete commands have been generated.");
            _quoteSuffix = value!;
        }
    }

    public override void RefreshSchema()
    {
        _tableName = string.Empty;
        _schemaTable = null;
        CreateNewCommand(ref _deleteCommand);
        CreateNewCommand(ref _updateCommand);
        CreateNewCommand(ref _insertCommand);
    }

    protected override void SetRowUpdatingHandler(DbDataAdapter adapter)
    {
        if (adapter is not SqliteDataAdapter dataAdapter)
        {
            throw new InvalidOperationException("Adapter needs to be a SqliteDataAdapter");
        }

        _rowUpdatingHandler = RowUpdatingHandler;
        dataAdapter.RowUpdating += _rowUpdatingHandler;
    }

    protected override void ApplyParameterInfo(DbParameter dbParameter,
        DataRow row,
        StatementType statementType,
        bool whereClause)
    {
    }

    protected override string GetParameterName(int position)
    {
        return $"?p{position}";
    }

    protected override string GetParameterName(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
            throw new ArgumentException("parameterName cannot be null or empty");
        if (parameterName[0] == '?')
            return parameterName;
        return $"?{parameterName}";
    }

    protected override string GetParameterPlaceholder(int position)
    {
        return $"?p{position}";
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _insertCommand?.Dispose();
                _deleteCommand?.Dispose();
                _updateCommand?.Dispose();
                _schemaTable?.Dispose();
            }

            _disposed = true;
        }
    }
    
    private SqliteCommand? SourceCommand => _dataAdapter?.SelectCommand;

    private string? QuotedTableName => GetQuotedString(_tableName);

    private new SqliteCommand? GetDeleteCommand()
    {
        BuildCache(closeConnection: true);
        if (_deleteCommand == null)
            return CreateDeleteCommand(false);
        return _deleteCommand;
    }

    private new SqliteCommand? GetInsertCommand()
    {
        BuildCache(closeConnection: true);
        if (_insertCommand == null)
            return CreateInsertCommand(false);
        return _insertCommand;
    }

    private new SqliteCommand? GetUpdateCommand()
    {
        BuildCache(closeConnection: true);
        if (_updateCommand == null)
            return CreateUpdateCommand(false);
        return _updateCommand;
    }
    
    private void BuildCache(bool closeConnection)
    {
        var sourceCommand = SourceCommand;
        if (sourceCommand == null)
            throw new InvalidOperationException("The DataAdapter.SelectCommand property needs to be initialized.");
        if (sourceCommand.Connection is not { } connection)
            throw new InvalidOperationException("The DataAdapter.SelectCommand.Connection property needs to be initialized.");

        if (_schemaTable == null)
        {
            if (connection.State == ConnectionState.Open)
                closeConnection = false;
            else
                connection.Open();

            var reader = sourceCommand.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo);
            _schemaTable = reader.GetSchemaTable();
            reader.Close();
            if (closeConnection)
                connection.Close();
            BuildInformation(_schemaTable);
        }
    }

    private void BuildInformation(DataTable schemaTable)
    {
        _tableName = string.Empty;
        foreach (DataRow schemaRow in schemaTable.Rows)
        {
            if (schemaRow.IsNull("BaseTableName") ||
                (string)schemaRow["BaseTableName"] == string.Empty)
                continue;

            if (_tableName == string.Empty)
                _tableName = (string)schemaRow["BaseTableName"];
            else if (_tableName != (string)schemaRow["BaseTableName"])
                throw new InvalidOperationException("Dynamic SQL generation is not supported against multiple base tables.");
        }

        if (_tableName == string.Empty)
            throw new InvalidOperationException("Dynamic SQL generation is not supported with no base table.");
        _schemaTable = schemaTable;
    }

    private SqliteCommand? CreateInsertCommand(bool option)
    {
        if (QuotedTableName == string.Empty)
            return null;

        CreateNewCommand(ref _insertCommand);

        var command = $"INSERT INTO {QuotedTableName}";
        var columns = new StringBuilder();
        var values = new StringBuilder();

        var parmIndex = 1;
        foreach (DataRow schemaRow in _schemaTable!.Rows)
        {
            if (!IncludedInInsert(schemaRow))
                continue;

            if (parmIndex > 1)
            {
                columns.Append(", ");
                values.Append(", ");
            }

            SqliteParameter? parameter;
            if (option)
            {
                parameter = _insertCommand!.Parameters.Add(CreateParameter(schemaRow));
            }
            else
            {
                parameter = _insertCommand!.Parameters.Add(CreateParameter(parmIndex++, schemaRow));
            }

            parameter.SourceVersion = DataRowVersion.Current;
            columns.Append(GetQuotedString(parameter.SourceColumn));
            values.Append(parameter.ParameterName);
        }

        var sql = $"{command} ({columns}) VALUES ({values})";
        _insertCommand!.CommandText = sql;
        return _insertCommand;
    }

    private SqliteCommand? CreateDeleteCommand(bool option)
    {
        if (QuotedTableName == string.Empty)
            return null;

        CreateNewCommand(ref _deleteCommand);

        var command = $"DELETE FROM {QuotedTableName}";
        var whereClause = new StringBuilder();
        var keyFound = false;
        var parmIndex = 1;

        foreach (DataRow schemaRow in _schemaTable!.Rows)
        {
            if ((bool)schemaRow["IsExpression"])
                continue;
            if (!IncludedInWhereClause(schemaRow))
                continue;

            if (whereClause.Length > 0)
                whereClause.Append(" AND ");

            var isKey = (bool)schemaRow["IsKey"];
            SqliteParameter? parameter;

            if (isKey)
                keyFound = true;

            var allowNull = (bool)schemaRow["AllowDBNull"];
            if (!isKey && allowNull)
            {
                if (option)
                {
                    parameter = _deleteCommand!.Parameters.Add($"@{schemaRow["BaseColumnName"]}", SqliteType.Integer);
                }
                else
                {
                    parameter = _deleteCommand!.Parameters.Add($"@p{parmIndex++}", SqliteType.Integer);
                }

                var sourceColumnName = (string)schemaRow["BaseColumnName"];
                parameter.Value = 1;

                whereClause.Append('(');
                whereClause.Append(string.Format(Clause1, parameter.ParameterName, GetQuotedString(sourceColumnName)));
                whereClause.Append(" OR ");
            }

            if (option)
            {
                parameter = _deleteCommand!.Parameters.Add(CreateParameter(schemaRow));
            }
            else
            {
                parameter = _deleteCommand!.Parameters.Add(CreateParameter(parmIndex++, schemaRow));
            }

            parameter.SourceVersion = DataRowVersion.Original;

            whereClause.Append(string.Format(Clause2, GetQuotedString(parameter.SourceColumn), parameter.ParameterName));

            if (!isKey && allowNull)
                whereClause.Append(')');
        }

        if (!keyFound)
            throw new InvalidOperationException("Dynamic SQL generation for the DeleteCommand is not supported against a SelectCommand that does not return any key column information.");

        var sql = $"{command} WHERE ({whereClause})";
        _deleteCommand!.CommandText = sql;
        return _deleteCommand;
    }

    private SqliteCommand? CreateUpdateCommand(bool option)
    {
        if (QuotedTableName == string.Empty)
            return null;

        CreateNewCommand(ref _updateCommand);

        var command = $"UPDATE {QuotedTableName} SET ";
        var columns = new StringBuilder();
        var whereClause = new StringBuilder();
        var parmIndex = 1;
        var keyFound = false;

        foreach (DataRow schemaRow in _schemaTable!.Rows)
        {
            if (!IncludedInUpdate(schemaRow))
                continue;
            if (columns.Length > 0)
                columns.Append(", ");

            SqliteParameter? parameter;
            if (option)
            {
                parameter = _updateCommand!.Parameters.Add(CreateParameter(schemaRow));
            }
            else
            {
                parameter = _updateCommand!.Parameters.Add(CreateParameter(parmIndex++, schemaRow));
            }

            parameter.SourceVersion = DataRowVersion.Current;

            columns.Append($"{GetQuotedString(parameter.SourceColumn)} = {parameter.ParameterName}");
        }

        foreach (DataRow schemaRow in _schemaTable.Rows)
        {
            if ((bool)schemaRow["IsExpression"])
                continue;

            if (!IncludedInWhereClause(schemaRow))
                continue;

            if (whereClause.Length > 0)
                whereClause.Append(" AND ");

            var isKey = (bool)schemaRow["IsKey"];
            SqliteParameter? parameter;

            if (isKey)
                keyFound = true;

            var allowNull = (bool)schemaRow["AllowDBNull"];
            if (!isKey && allowNull)
            {
                if (option)
                {
                    parameter = _updateCommand!.Parameters.Add($"@{schemaRow["BaseColumnName"]}", SqliteType.Integer);
                }
                else
                {
                    parameter = _updateCommand!.Parameters.Add($"@p{parmIndex++}", SqliteType.Integer);
                }

                parameter.Value = 1;
                whereClause.Append('(');
                whereClause.Append(string.Format(Clause1, parameter.ParameterName, GetQuotedString((string)schemaRow["BaseColumnName"])));
                whereClause.Append(" OR ");
            }

            if (option)
            {
                parameter = _updateCommand!.Parameters.Add(CreateParameter(schemaRow));
            }
            else
            {
                parameter = _updateCommand!.Parameters.Add(CreateParameter(parmIndex++, schemaRow));
            }

            parameter.SourceVersion = DataRowVersion.Original;
            whereClause.Append(string.Format(Clause2, GetQuotedString(parameter.SourceColumn), parameter.ParameterName));

            if (!isKey && allowNull)
                whereClause.Append(')');
        }

        if (!keyFound)
            throw new InvalidOperationException("Dynamic SQL generation for the UpdateCommand is not supported against a SelectCommand that does not return any key column information.");

        var sql = $"{command}{columns} WHERE ({whereClause})";
        _updateCommand!.CommandText = sql;
        return _updateCommand;
    }

    private void CreateNewCommand(ref SqliteCommand? command)
    {
        SqliteCommand? sourceCommand = SourceCommand;
        if (command == null)
        {
            command = sourceCommand!.Connection!.CreateCommand();
            command.CommandTimeout = sourceCommand.CommandTimeout;
            command.Transaction = sourceCommand.Transaction;
        }

        command.CommandType = CommandType.Text;
        command.UpdatedRowSource = UpdateRowSource.None;
        command.Parameters.Clear();
    }

    private static bool IncludedInWhereClause(DataRow schemaRow)
    {
        if (!schemaRow.IsNull("IsLong") && (bool)schemaRow["IsLong"])
            return false;
        return true;
    }

    private static bool IncludedInInsert(DataRow schemaRow)
    {
        if (!schemaRow.IsNull("IsAutoIncrement") && (bool)schemaRow["IsAutoIncrement"])
            return false;
        if (!schemaRow.IsNull("IsExpression") && (bool)schemaRow["IsExpression"])
            return false;
        return true;
    }

    private static bool IncludedInUpdate(DataRow schemaRow)
    {
        if (!schemaRow.IsNull("IsAutoIncrement") && (bool)schemaRow["IsAutoIncrement"])
            return false;
        if (!schemaRow.IsNull("IsExpression") && (bool)schemaRow["IsExpression"])
            return false;

        return true;
    }

    private static SqliteParameter CreateParameter(DataRow schemaRow)
    {
        var sourceColumn = (string)schemaRow["BaseColumnName"];
        var name = $"@{sourceColumn}";
        var dbType = (string)schemaRow["DataTypeName"];
        var size = (int)schemaRow["ColumnSize"];

        return new SqliteParameter(name, Enum.Parse<SqliteType>(dbType, ignoreCase: true), size, sourceColumn);
    }

    private static SqliteParameter CreateParameter(int parmIndex, DataRow schemaRow)
    {
        var name = $"@p{parmIndex}";
        var sourceColumn = (string)schemaRow["BaseColumnName"];
        var dbType = (string)schemaRow["DataTypeName"];
        var size = (int)schemaRow["ColumnSize"];

        return new SqliteParameter(name, Enum.Parse<SqliteType>(dbType, ignoreCase: true), size, sourceColumn);
    }

    private string? GetQuotedString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        if (string.IsNullOrEmpty(_quotePrefix) && string.IsNullOrEmpty(_quoteSuffix))
            return value;
        return $"{_quotePrefix}{value}{_quoteSuffix}";
    }

    private void RowUpdatingHandler(object sender, RowUpdatingEventArgs args)
    {
        if (args.Command != null)
            return;
        try
        {
            switch (args.StatementType)
            {
                case StatementType.Insert:
                    args.Command = GetInsertCommand();
                    break;
                case StatementType.Update:
                    args.Command = GetUpdateCommand();
                    break;
                case StatementType.Delete:
                    args.Command = GetDeleteCommand();
                    break;
            }
        }
        catch (Exception e)
        {
            args.Errors = e;
            args.Status = UpdateStatus.ErrorsOccurred;
        }
    }
}