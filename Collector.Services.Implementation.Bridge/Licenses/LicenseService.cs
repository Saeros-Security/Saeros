using Collector.Core.Hubs.Licenses;
using Collector.Services.Abstractions.Licenses;
using Collector.Services.Implementation.Bridge.Helpers;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector.Repositories.Licences;
using Shared.Helpers;
using Streaming;

namespace Collector.Services.Implementation.Bridge.Licenses;

public sealed class LicenseService(ILogger<LicenseService> logger, IStreamingLicenseHub streamingLicenseHub, ICollectorLicenseRepository licenseRepository) : ILicenseService
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        LicenseHelper.Initialize(licenseRepository);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            Execute();
        }
    }
    
    private void Execute()
    {
        try
        {
            var valid = LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature(), out var expiresAt);
            streamingLicenseHub.SendLicenseExpiration(new LicenseExpirationContract
            {
                Expired = !valid,
                ExpiresAt = expiresAt.UtcTicks
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not fetch license");
        }
    }
}