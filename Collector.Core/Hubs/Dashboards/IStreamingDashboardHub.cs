using Shared.Streaming.Interfaces;
using Streaming;

namespace Collector.Core.Hubs.Dashboards;

public interface IStreamingDashboardHub : IDashboardForwarder
{
    void SendDashboard(DashboardContract dashboardContract);
}