namespace Collector.Databases.Abstractions.Stores.Authentication;

public interface IAuthenticationStore
{
    ValueTask<ISet<string>> GetAuthorizationValuesAsync(CancellationToken cancellationToken);
    ValueTask<string> CreateAuthorizationValueAsync(string username, CancellationToken cancellationToken);
}