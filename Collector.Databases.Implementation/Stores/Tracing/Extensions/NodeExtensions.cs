using System.Text.Json;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Processes;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;
using Collector.Databases.Implementation.Stores.Tracing.Buckets.Workstations;
using Collector.Databases.Implementation.Stores.Tracing.Data;
using Collector.Databases.Implementation.Stores.Tracing.Helpers;
using QuikGraph;

namespace Collector.Databases.Implementation.Stores.Tracing.Extensions;

internal static class NodeExtensions
{
    private static TracingNode ToNode<T>(string value, T data, DateTimeOffset date) where T : TracingData
    {
        return new TracingNode(value, jsonUserData: JsonSerializer.Serialize(data), date.Ticks);
    }
    
    public static TracingNode ToTracingNode(this Process process, ProcessTree processTree)
    {
        return ToNode($"[{process.Domain}] {process.WorkstationName} ¦ {process.ProcessName} ({process.ProcessId})", new ProcessData(process.Domain, process.WorkstationName, user: process.Username, process.UserSid, process.ProcessId, process.ProcessName, processTree.Value, process.CommandLine, process.ParentProcessName, process.Elevated), process.Date);
    }

    public static TracingNode ToTracingNode(this Workstation workstation, ILogonStore logonStore)
    {
        if (logonStore.TryGetOperatingSystemByComputer(workstation.WorkstationName, out var operatingSystem))
        {
            return ToNode($"[{workstation.Domain}] {workstation.WorkstationName}", new WorkstationData(workstation.Domain, ipAddress: workstation.IpAddress, workstation.WorkstationName, operatingSystem), workstation.Date);
        }
        else
        {
            return ToNode($"[{workstation.Domain}] {workstation.WorkstationName}", new WorkstationData(workstation.Domain, ipAddress: workstation.IpAddress, workstation.WorkstationName, operatingSystem: "Unknown"), workstation.Date);
        }
    }

    public static TracingNode ToTracingNode(this User user, IGeolocationService geolocationService)
    {
        if (geolocationService.TryResolve(user.SourceIp, out var countryCode, out var asn))
        {
            var userData = new UserData(user.Sid, user.Domain, user.Name, logonType: GetLogonType(user), user.Privileged, sourceIp: $"{user.SourceIp} ({countryCode} | {asn})", user.SourceHostname, user.Logon);
            return ToNode($"[{user.Domain}] {user.Sid} {user.LogonId}", data: userData, user.Date);
        }
        else
        {
            var userData = new UserData(user.Sid, user.Domain, user.Name, logonType: GetLogonType(user), user.Privileged, sourceIp: user.SourceIp, user.SourceHostname, user.Logon);
            return ToNode($"[{user.Domain}] {user.Sid} {user.LogonId}", data: userData, user.Date);
        }
    }

    private static string GetLogonType(User user)
    {
        return user.LogonType == -1 ? "Unknown" : LogonTypes.ReversedTypes.GetValueOrDefault((LogonType)user.LogonType, "Unknown");
    }
}