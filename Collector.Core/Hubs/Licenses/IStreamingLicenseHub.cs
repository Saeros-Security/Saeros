using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.Licenses;

public interface IStreamingLicenseHub : ILicenseForwarder
{
    void SendLicenseExpiration(LicenseExpirationContract licenseExpirationContract);
}