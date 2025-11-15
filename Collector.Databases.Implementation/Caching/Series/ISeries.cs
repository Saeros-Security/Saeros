namespace Collector.Databases.Implementation.Caching.Series;

public interface ISeries : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
}