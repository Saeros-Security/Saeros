using Collector.Databases.Abstractions.Domain.Profiles;
using Collector.Databases.Abstractions.Domain.Settings;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Streaming;

namespace Collector.Databases.Abstractions.Repositories.Settings;

public interface ISettingsRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<TimeSpan> GetRetentionAsync(CancellationToken cancellationToken);
    Task<Shared.Models.Console.Responses.Settings> GetSettingsAsync(CancellationToken cancellationToken);
    Task<ProfileChangeRecord> SetProfileAsync(DetectionProfile profile, CancellationToken cancellationToken);
    Task<SettingsChangeRecord> SetSettingsAsync(SetSettings settings, CancellationToken cancellationToken);
    IAsyncEnumerable<DomainRecord> EnumerateDomainsAsync(CancellationToken cancellationToken);
    Task AddDomainAsync(DomainRecord domain, CancellationToken cancellationToken);
    Task DeleteDomainAsync(string name, CancellationToken cancellationToken);
    ValueTask StoreAsync(MetricContract metricContract, CancellationToken cancellationToken);
}