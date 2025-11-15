using System.Threading.Channels;
using Streaming;

namespace Collector.Core.Hubs.Licenses;

public sealed class StreamingLicenseHub : IStreamingLicenseHub
{
    public void SendLicenseExpiration(LicenseExpirationContract licenseExpirationContract)
    {
        LicenseChannel.Writer.TryWrite(licenseExpirationContract);
    }

    public Channel<LicenseExpirationContract> LicenseChannel { get; } = Channel.CreateBounded<LicenseExpirationContract>(new BoundedChannelOptions(capacity: 1)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });
}