using System.Threading.Channels;
using Streaming;

namespace Collector.Core.Hubs.Dashboards;

public sealed class StreamingDashboardHub : IStreamingDashboardHub
{
    public void SendDashboard(DashboardContract dashboardContract)
    {
        DashboardChannel.Writer.TryWrite(dashboardContract);
    }

    public Channel<DashboardContract> DashboardChannel { get; } = Channel.CreateBounded<DashboardContract>(new BoundedChannelOptions(capacity: 1)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });
}