using System.Diagnostics.CodeAnalysis;
using Collector.Databases.Abstractions.Repositories.AuditPolicies;
using Collector.Databases.Implementation.Contexts.AuditPolicies;
using Collector.Databases.Implementation.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Collector.Databases.Implementation.Repositories.AuditPolicies;

public sealed class AuditPoliciesRepository(AuditPoliciesContext context) : IAuditPoliciesRepository
{
    public void AddBackup(byte[] content, bool advancedAuditPoliciesEnabled)
    {
        try
        {
            if (content.Length == 0) return;
            using var connection = context.CreateSingleConnection();
            connection.DbConnection.Open();
            using var command = connection.DbConnection.CreateCommand();
            command.CommandText =
                @"
            DELETE FROM AuditPolicies;

            INSERT INTO AuditPolicies (Backup, AdvancedAuditPoliciesEnabled)
            VALUES (@Backup, @AdvancedAuditPoliciesEnabled);
";
            command.Parameters.Add(new SqliteParameter("Backup", DatabaseHelper.GetValue(content)));
            command.Parameters.Add(new SqliteParameter("AdvancedAuditPoliciesEnabled", DatabaseHelper.GetValue(advancedAuditPoliciesEnabled)));
            command.ExecuteNonQuery();
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

    public bool TryGetBackup([MaybeNullWhen(false)] out byte[] content, out bool advancedAuditPoliciesEnabled)
    {
        content = null;
        advancedAuditPoliciesEnabled = false;
        try
        {
            using var connection = context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                @"
            SELECT Backup, AdvancedAuditPoliciesEnabled FROM AuditPolicies;
";
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                content = reader.GetFieldValue<byte[]>(0);
                advancedAuditPoliciesEnabled = reader.GetBoolean(1);
                return true;
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "An error has occurred");
        }

        return false;
    }
}