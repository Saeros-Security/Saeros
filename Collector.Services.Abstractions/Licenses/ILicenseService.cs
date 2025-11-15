namespace Collector.Services.Abstractions.Licenses;

public interface ILicenseService
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}