namespace Collector.Services.Abstractions.EventProviders;

public interface IEventProviderServiceWriter
{
    void LoadProviders(CancellationToken cancellationToken);
}