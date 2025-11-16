using System.Diagnostics.CodeAnalysis;
using Collector.Databases.Abstractions.Domain.Users;
using Collector.Databases.Abstractions.Repositories.Users;
using Collector.Databases.Implementation.Contexts.Users;
using Collector.Databases.Implementation.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Collector.Databases.Implementation.Repositories.Users;

public sealed class UserRepository(UserContext userContext) : IUserRepository
{
    public IEnumerable<UserRecord> GetUsers()
    {
        var users = new List<UserRecord>();
        using var connection = userContext.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Username FROM Users;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            if (TryGetUserByName(name, out var user))
            {
                users.Add(user);
            }
        }

        return users;
    }

    public bool TryGetUserByName(string username, [MaybeNullWhen(false)] out UserRecord user)
    {
        user = null;
        try
        {
            using var connection = userContext.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                @"
            SELECT Username, PasswordHash, Id
            FROM Users
            WHERE Username = @Username;
";

            command.Parameters.Add(new SqliteParameter("Username", DatabaseHelper.GetValue(username)));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var hash = reader.GetString(1);
                var id = reader.GetInt32(2);
                user = new UserRecord(id, name, hash);
                return true;
            }

            return false;
        }
        
        catch (Exception ex)
        {
            userContext.Logger.LogError(ex, "An error has occurred");
            throw;
        }
    }

    public UserRecord CreateUser(UserRecord user)
    {
        if (TryGetUserByName(user.Username, out var foundUser))
        {
            return foundUser;
        }

        using var connection = userContext.CreateSingleConnection();
        connection.DbConnection.Open();
        using var command = connection.DbConnection.CreateCommand();
        command.CommandText =
            @"
            INSERT INTO Users (Username, PasswordHash)
            VALUES (@Username, @PasswordHash);
";
        command.Parameters.Add(new SqliteParameter("Username", DatabaseHelper.GetValue(user.Username)));
        command.Parameters.Add(new SqliteParameter("PasswordHash", DatabaseHelper.GetValue(user.PasswordHash)));
        command.ExecuteNonQuery();

        TryGetUserByName(user.Username, out var newUser);
        return newUser!;
    }

    public UserRecord ChangeUserPassword(UserRecord user)
    {
        using var connection = userContext.CreateSingleConnection();
        connection.DbConnection.Open();
        using var command = connection.DbConnection.CreateCommand();
        command.CommandText =
            @"
            UPDATE Users 
            SET PasswordHash = @PasswordHash
            WHERE Username = @Username;
";
        command.Parameters.Add(new SqliteParameter("Username", DatabaseHelper.GetValue(user.Username)));
        command.Parameters.Add(new SqliteParameter("PasswordHash", DatabaseHelper.GetValue(user.PasswordHash)));
        command.ExecuteNonQuery();
        return user;
    }

    public async ValueTask<string> CreateUserToken(string username, CancellationToken cancellationToken)
    {
        using var connection = await userContext.CreateConnectionAsync(cancellationToken);
        await connection.DbConnection.OpenAsync(cancellationToken);
        await using var command = connection.DbConnection.CreateCommand();
        command.CommandText =
            @"
            UPDATE Users 
            SET UserToken = @UserToken
            WHERE Username = @Username;
";
        var token = Guid.NewGuid().ToString();
        command.Parameters.Add(new SqliteParameter("Username", DatabaseHelper.GetValue(username)));
        command.Parameters.Add(new SqliteParameter("UserToken", DatabaseHelper.GetValue(token)));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return token;
    }

    public async ValueTask<ISet<string>> GetUserTokens(CancellationToken cancellationToken)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var connection = userContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                @"
            SELECT UserToken FROM Users;
";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0)) continue;
                tokens.Add(reader.GetString(0));
            }
        }
        catch (OperationCanceledException)
        {
            userContext.Logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            userContext.Logger.LogError(ex, "An error has occurred");
        }
        
        return tokens;
    }
}