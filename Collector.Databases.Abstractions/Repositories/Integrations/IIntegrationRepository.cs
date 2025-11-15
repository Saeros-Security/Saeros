using System.Reactive;
using Shared.Integrations;
using Shared.Models.Console.Requests;

namespace Collector.Databases.Abstractions.Repositories.Integrations;

public interface IIntegrationRepository : IDisposable
{
    Task UpdateIntegrationAsync(UpdateIntegration updateIntegration, CancellationToken cancellationToken);
    Task<IEnumerable<IntegrationBase>> GetIntegrationsAsync(CancellationToken cancellationToken);
    IObservable<Unit> IntegrationChanged { get; }
    Task SetStatusAsync(int id, IntegrationStatus status, CancellationToken cancellationToken);
    void OnIntegrationChanged();
}