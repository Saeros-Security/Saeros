using System.Diagnostics.CodeAnalysis;
using Collector.Databases.Abstractions.Domain.Users;

namespace Collector.Databases.Abstractions.Repositories.Users;

public interface IUserRepository
{
    IEnumerable<UserRecord> GetUsers();
    UserRecord CreateUser(UserRecord user);
    bool TryGetUserByName(string username, [MaybeNullWhen(false)] out UserRecord user);
    UserRecord ChangeUserPassword(UserRecord user);
    ValueTask<string> CreateUserToken(string username, CancellationToken cancellationToken);
    ValueTask<ISet<string>> GetUserTokens(CancellationToken cancellationToken);
}