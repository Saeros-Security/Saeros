using System.Net;
using System.Text.Json.Serialization;
using Collector.Core.Extensions;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Extensions;
using Collector.Databases.Implementation.Helpers;
using Collector.Databases.Implementation.Stores.Tracing.Helpers;
using Shared;
using Shared.Helpers;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;

public class Logon4625(string domain, string workstationName, DateTime date, string subjectUserSid, string subjectUserName, string subjectDomainName, string subjectLogonId, string targetUserSid, string targetUserName, string targetDomainName, string logonType, string ipAddress, bool userPrivileged, string domainControllerName, string domainControllerIpAddress) : LogonTracer(domain, workstationName, date), IEquatable<Logon4625>
{
    public static async ValueTask<Tracer> ToLogonAsync(WinEvent winEvent, ILogonStore logonStore, CancellationToken cancellationToken)
    {
        var targetUserName = GetProperty(winEvent, nameof(TargetUserName));
        var targetComputer = GetProperty(winEvent, nameof(WorkstationName));
        var logonType = GetProperty(winEvent, nameof(LogonType));
        var ipAddress = GetProperty(winEvent, nameof(IpAddress));
        if (!logonStore.TryGetWorkstationNameFromIpAddress(ipAddress, out var workstationName))
        {
            workstationName = string.Empty;
        }
        
        if (int.TryParse(logonType, out var type))
        {
            if (!targetUserName.Equals("-") && !targetUserName.EndsWith('$') && !string.IsNullOrEmpty(ipAddress) && !ipAddress.Equals("-") && IPAddress.TryParse(ipAddress, out var address) && !address.IsPrivate())
            {
                var accountLogon = new AccountLogon(targetUserName, targetComputer.Equals("-") ? winEvent.Computer : targetComputer, LogonTypes.ReversedTypes.TryGetValue((LogonType)type, out var logon) ? logon : string.Empty, workstationName, ipAddress);
                logonStore.AddFailureLogon(accountLogon);
            }

            if (type == 3)
            {
                return DefaultTracer.Instance; // We're not interested in plain network authentication type because effective logon session (either Interactive or RemoteInteractive) will not happen in there
            }
        }
        
        if (!winEvent.EventData.TryGetValue(nameof(TargetUserName), out var userName) || !logonStore.TryGetSidByUser(userName, out var targetUserSid))
        {
            targetUserSid = WellKnownSids.Nobody.Sid;
        }
        
        var subjectUserSid = GetProperty(winEvent, nameof(SubjectUserSid));
        if (subjectUserSid.IsKnownSid() && targetUserSid.IsKnownSid()) return DefaultTracer.Instance;
        
        var domainControllerName = GetProperty(winEvent, nameof(WorkstationName));
        if (domainControllerName.Equals("-", StringComparison.Ordinal))
        {
            domainControllerName = winEvent.Computer;
        }
        
        var domainControllerIpAddress = await IpAddressResolver.GetIpAddressAsync(domainControllerName, cancellationToken);
        return new Logon4625(DomainHelper.DomainName,
            workstationName,
            winEvent.SystemTime.ToUniversalTime(),
            subjectUserSid,
            GetProperty(winEvent, nameof(SubjectUserName)),
            GetProperty(winEvent, nameof(SubjectDomainName)),
            GetProperty(winEvent, nameof(SubjectLogonId)),
            targetUserSid,
            GetProperty(winEvent, nameof(TargetUserName)),
            GetProperty(winEvent, nameof(TargetDomainName)),
            logonType,
            GetProperty(winEvent, nameof(IpAddress)),
            logonStore.IsUserPrivileged(winEvent),
            domainControllerName,
            domainControllerIpAddress);
    }

    [JsonPropertyName("SubjectUserSid")]
    public string SubjectUserSid { get; } = subjectUserSid;

    [JsonPropertyName("SubjectUserName")]
    public string SubjectUserName { get; } = subjectUserName;

    [JsonPropertyName("SubjectDomainName")]
    public string SubjectDomainName { get; } = subjectDomainName;

    [JsonPropertyName("SubjectLogonId")]
    public string SubjectLogonId { get; } = subjectLogonId;

    [JsonPropertyName("TargetUserSid")]
    public string TargetUserSid { get; } = targetUserSid;

    [JsonPropertyName("TargetUserName")]
    public string TargetUserName { get; } = targetUserName;

    [JsonPropertyName("TargetDomainName")]
    public string TargetDomainName { get; } = targetDomainName;

    [JsonPropertyName("LogonType")]
    public string LogonType { get; } = logonType;

    [JsonPropertyName("IpAddress")]
    public string IpAddress { get; } = ipAddress;
    
    [JsonPropertyName("UserPrivileged")]
    public bool UserPrivileged { get; } = userPrivileged;
    
    [JsonPropertyName("DomainControllerName")]
    public string DomainControllerName { get; } = domainControllerName;
    
    [JsonPropertyName("DomainControllerIpAddress")]
    public string DomainControllerIpAddress { get; } = domainControllerIpAddress;

    public bool Equals(Logon4625? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return TargetUserName.Equals(other.TargetUserName, StringComparison.OrdinalIgnoreCase) && TargetDomainName.Equals(other.TargetDomainName, StringComparison.OrdinalIgnoreCase) && IpAddress.Equals(other.IpAddress, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Logon4625)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(TargetUserName, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(TargetDomainName, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(IpAddress, StringComparer.OrdinalIgnoreCase);
        return hashCode.ToHashCode();
    }
}