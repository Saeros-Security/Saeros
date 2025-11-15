using System.Net;
using System.Text.Json.Serialization;
using Collector.Core.Extensions;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Extensions;
using Collector.Databases.Implementation.Helpers;
using Collector.Databases.Implementation.Stores.Tracing.Helpers;
using Shared;
using Shared.Helpers;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;

public class Logon4624(string domain, string workstationName, DateTime date, string subjectUserSid, string subjectUserName, string subjectDomainName, string subjectLogonId, string targetUserSid, string targetUserName, string targetDomainName, string targetLogonId, string logonType, string processName, string ipAddress, string workstationIpAddress, bool userPrivileged) : LogonTracer(domain, workstationName, date)
{
    public static async ValueTask<Tracer> ToLogonAsync(WinEvent winEvent, ILogonStore logonStore, CancellationToken cancellationToken)
    {
        var targetUserName = GetProperty(winEvent, nameof(TargetUserName));
        var targetComputer = GetProperty(winEvent, nameof(WorkstationName));
        var logonType = GetProperty(winEvent, nameof(LogonType));
        var ipAddress = GetProperty(winEvent, nameof(IpAddress));
        if (int.TryParse(logonType, out var type))
        {
            if (!targetUserName.Equals("-") && !targetUserName.EndsWith('$') && !string.IsNullOrEmpty(ipAddress) && !ipAddress.Equals("-") && IPAddress.TryParse(ipAddress, out var address) && !address.IsPrivate())
            {
                var accountLogon = new AccountLogon(targetUserName, targetComputer.Equals("-") ? winEvent.Computer : targetComputer, LogonTypes.ReversedTypes.TryGetValue((LogonType)type, out var logon) ? logon : string.Empty, logonStore.TryGetWorkstationNameFromIpAddress(ipAddress, out var sourceComputer) ? sourceComputer : string.Empty, ipAddress);
                logonStore.AddSuccessLogon(accountLogon);
            }
            
            if (type == 3)
            {
                return DefaultTracer.Instance; // We're not interested in plain network authentication type because effective logon session (either Interactive or RemoteInteractive) will not happen in there
            }
        }
        
        var subjectUserSid = GetProperty(winEvent, nameof(SubjectUserSid));
        var targetUserSid = GetProperty(winEvent, nameof(TargetUserSid));
        if (subjectUserSid.IsKnownSid() && targetUserSid.IsKnownSid()) return DefaultTracer.Instance;
        
        var subjectLogonId = GetProperty(winEvent, nameof(SubjectLogonId));
        var targetLogonId = GetProperty(winEvent, nameof(TargetLogonId));
        if (string.IsNullOrEmpty(subjectLogonId) || string.IsNullOrEmpty(targetLogonId) || (LogonHelper.FromLogonId(subjectLogonId) == 0L && LogonHelper.FromLogonId(targetLogonId) == 0L)) return DefaultTracer.Instance;
        
        var workstationIpAddress = await IpAddressResolver.GetIpAddressFrom4624Async(winEvent, cancellationToken);
        if (!logonStore.TryGetWorkstationNameFromIpAddress(workstationIpAddress, out var workstationName))
        {
            workstationName = winEvent.GetWorkstationName();
        }
        
        return new Logon4624(DomainHelper.DomainName,
            workstationName,
            winEvent.SystemTime.ToUniversalTime(),
            subjectUserSid,
            GetProperty(winEvent, nameof(SubjectUserName)),
            GetProperty(winEvent, nameof(SubjectDomainName)),
            subjectLogonId,
            targetUserSid,
            targetUserName,
            GetProperty(winEvent, nameof(TargetDomainName)),
            targetLogonId,
            logonType,
            GetProperty(winEvent, nameof(ProcessName)),
            ipAddress,
            workstationIpAddress,
            logonStore.IsUserPrivileged(winEvent));
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

    [JsonPropertyName("TargetLogonId")]
    public string TargetLogonId { get; } = targetLogonId;

    [JsonPropertyName("LogonType")]
    public string LogonType { get; } = logonType;
    
    [JsonPropertyName("ProcessName")]
    public string ProcessName { get; } = processName;
    
    [JsonPropertyName("IpAddress")]
    public string IpAddress { get; } = ipAddress;

    [JsonPropertyName("WorkstationIpAddress")]
    public string WorkstationIpAddress { get; } = workstationIpAddress;
    
    [JsonPropertyName("UserPrivileged")]
    public bool UserPrivileged { get; } = userPrivileged;
}