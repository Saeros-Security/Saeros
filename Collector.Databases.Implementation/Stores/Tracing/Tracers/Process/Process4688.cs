using System.Text.Json.Serialization;
using Collector.Core.Extensions;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Extensions;
using Collector.Databases.Implementation.Helpers;
using Shared;
using Shared.Helpers;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;

public class Process4688(string domain, string workstationName, DateTime date, string subjectUserSid, string subjectUserName, string subjectDomainName, string subjectLogonId, string newProcessId, string newProcessName, string targetUserSid, string targetUserName, string targetDomainName, string targetLogonId, string tokenElevationType, string commandLine, string parentProcessName, string workstationIpAddress, bool userPrivileged) : ProcessTracer(domain, workstationName, date)
{
    public static async ValueTask<Tracer> ToProcessAsync(WinEvent winEvent, ILogonStore logonStore, Action<uint, string, DateTime> onCreation, CancellationToken cancellationToken)
    {
        var subjectUserSid = GetProperty(winEvent, nameof(SubjectUserSid));
        var targetUserSid = GetProperty(winEvent, nameof(TargetUserSid));
        if (subjectUserSid.IsKnownSid() && targetUserSid.IsKnownSid()) return DefaultTracer.Instance;
        if (!winEvent.EventData.TryGetValue(nameof(NewProcessName), out var pName)) return DefaultTracer.Instance;
        if (!winEvent.EventData.TryGetValue(nameof(NewProcessId), out var pId)) return DefaultTracer.Instance;
        if (pName.Equals("-", StringComparison.Ordinal)) return DefaultTracer.Instance;
        
        onCreation(pId.ParseUnsigned(), pName, winEvent.SystemTime.ToUniversalTime());
        var workstation = winEvent.GetWorkstationName();
        return new Process4688(DomainHelper.DomainName,
            workstation,
            winEvent.SystemTime.ToUniversalTime(),
            subjectUserSid,
            GetProperty(winEvent, nameof(SubjectUserName)),
            GetProperty(winEvent, nameof(SubjectDomainName)),
            GetProperty(winEvent, nameof(SubjectLogonId)),
            pId,
            pName,
            targetUserSid,
            GetProperty(winEvent, nameof(TargetUserName)),
            GetProperty(winEvent, nameof(TargetDomainName)),
            GetProperty(winEvent, nameof(TargetLogonId)),
            GetProperty(winEvent, nameof(TokenElevationType)),
            GetProperty(winEvent, nameof(CommandLine)),
            GetProperty(winEvent, nameof(ParentProcessName)),
            await IpAddressResolver.GetIpAddressAsync(workstation, cancellationToken),
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

    [JsonPropertyName("NewProcessId")]
    public string NewProcessId { get; } = newProcessId;

    [JsonPropertyName("NewProcessName")]
    public string NewProcessName { get; } = newProcessName;
    
    [JsonPropertyName("TargetUserSid")]
    public string TargetUserSid { get; } = targetUserSid;

    [JsonPropertyName("TargetUserName")]
    public string TargetUserName { get; } = targetUserName;

    [JsonPropertyName("TargetDomainName")]
    public string TargetDomainName { get; } = targetDomainName;

    [JsonPropertyName("TargetLogonId")]
    public string TargetLogonId { get; } = targetLogonId;
    
    [JsonPropertyName("TokenElevationType")]
    public string TokenElevationType { get; } = tokenElevationType;
    
    [JsonPropertyName("CommandLine")]
    public string CommandLine { get; } = commandLine;
    
    [JsonPropertyName("ParentProcessName")]
    public string ParentProcessName { get; } = parentProcessName;
    
    [JsonPropertyName("WorkstationIpAddress")]
    public string WorkstationIpAddress { get; } = workstationIpAddress;
    
    [JsonPropertyName("UserPrivileged")]
    public bool UserPrivileged { get; } = userPrivileged;
}