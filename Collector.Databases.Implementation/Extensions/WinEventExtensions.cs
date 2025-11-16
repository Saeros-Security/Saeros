using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;
using Shared;
using Shared.Extensions;
using Shared.Helpers;

namespace Collector.Databases.Implementation.Extensions;

public static class WinEventExtensions
{
    public static string GetWorkstationName(this WinEvent winEvent)
    {
        if (winEvent.EventData.TryGetValue(nameof(Process4688.SubjectLogonId), out var subjectLogonId))
        {
            var logonId = LogonHelper.FromLogonId(subjectLogonId);
            if (logonId > 0L)
            {
                if (winEvent.EventData.TryGetValue(nameof(Process4688.SubjectUserName), out var subjectUserName) && subjectUserName.EndsWith('$'))
                {
                    return subjectUserName.StripDollarSign();
                }
                
                if (winEvent.EventData.TryGetValue(nameof(Process4688.SubjectUserSid), out var subjectUserSid) &&
                    winEvent.EventData.TryGetValue(nameof(Process4688.SubjectDomainName), out var subjectDomainName))
                {
                    if (!subjectDomainName.Equals("-", StringComparison.Ordinal) &&
                        !DomainHelper.DomainName.Contains(subjectDomainName, StringComparison.OrdinalIgnoreCase) &&
                        !WellKnownSids.TryFindByBigramOrSid(subjectUserSid, out _))
                    {
                        return subjectDomainName;
                    }
                }
            }
        }

        if (winEvent.EventData.TryGetValue(nameof(Process4688.TargetLogonId), out var targetLogonId))
        {
            var logonId = LogonHelper.FromLogonId(targetLogonId);
            if (logonId > 0L)
            {
                if (winEvent.EventData.TryGetValue(nameof(Process4688.TargetUserName), out var targetUserName) && targetUserName.EndsWith('$'))
                {
                    return targetUserName.StripDollarSign();
                }
                
                if (winEvent.EventData.TryGetValue(nameof(Process4688.TargetUserSid), out var targetUserSid) &&
                    winEvent.EventData.TryGetValue(nameof(Process4688.TargetDomainName), out var targetDomainName))
                {
                    if (!targetDomainName.Equals("-", StringComparison.Ordinal) &&
                        !DomainHelper.DomainName.Contains(targetDomainName, StringComparison.OrdinalIgnoreCase) &&
                        !WellKnownSids.TryFindByBigramOrSid(targetUserSid, out _))
                    {
                        return targetDomainName;
                    }
                }
            }
        }

        return winEvent.Computer.StripDomain();
    }
}