using System.Diagnostics.Eventing.Reader;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Agent.EventLogs.Helpers;

internal static class ChannelHelper
{
    public static bool EnableChannel(ILogger logger, string channelName)
    {
        using var eventLogConfiguration = new EventLogConfiguration(channelName);
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User;
        if (user is null) return false;
        if (!AccessHelper.HaveAccess(new RawSecurityDescriptor(eventLogConfiguration.SecurityDescriptor), user, hasAccess: access => access.HasFlag(AccessHelper.AccessFlags.Read)))
        {
            logger.LogWarning("{Identity} does not have right to read the channel {Channel}", user.Value, channelName);
            return false;
        }

        if (eventLogConfiguration.IsEnabled)
        {
            return true;
        }

        logger.LogInformation("Enabling channel {Channel}...", channelName);
        eventLogConfiguration.IsEnabled = true;
        eventLogConfiguration.SaveChanges();
        return true;
    }
}