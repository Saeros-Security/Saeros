using Collector.Databases.Abstractions.Repositories.Users;
using Collector.Databases.Abstractions.Stores.Authentication;
using Collector.Databases.Implementation.Caching.LRU;

namespace Collector.Databases.Implementation.Stores.Authentication;

public sealed class AuthenticationStore(IUserRepository userRepository) : IAuthenticationStore
{
    public ValueTask<ISet<string>> GetAuthorizationValuesAsync(CancellationToken cancellationToken)
    {
        Lrus.UserTokensByUserName.Policy.ExpireAfterAccess.Value?.TrimExpired();
        return Lrus.UserTokensByUserName.GetOrAddAsync(string.Empty, valueFactory: async _ => await userRepository.GetUserTokens(cancellationToken));
    }

    public async ValueTask<string> CreateAuthorizationValueAsync(string username, CancellationToken cancellationToken)
    {
        var token = await userRepository.CreateUserToken(username, cancellationToken);
        Lrus.UserTokensByUserName.Clear();
        return token;
    }
}