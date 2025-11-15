namespace Collector.Services.Abstractions.Domains;

public interface IDomainService : IAsyncDisposable
{
    Task AddOrUpdateAsync(string username, string password, string domain, string primaryDomainController, int ldapPort, CancellationToken cancellationToken);
    Task DeleteAsync(string username, string password, string domain, string primaryDomainController, int ldapPort, CancellationToken cancellationToken);
    void Observe();
}