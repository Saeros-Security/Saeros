namespace Collector.Services.Abstractions.DomainControllers;

public interface IDomainControllerService
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}