using Microsoft.O365.Security.ETW;
using Shared;
using Shared.Extensions;
using Shared.Helpers;

namespace Collector.Services.Implementation.Agent.EventLogs.Extensions;

public static class EventRecordExtensions
{
    public static WinEvent BuildWinEvent(this EventRecord record, string channel, int propertyCount, out Dictionary<string, string> eventData)
    {
        var system = new Dictionary<string, string>(capacity: 8, StringComparer.OrdinalIgnoreCase);
        eventData = new Dictionary<string, string>(capacity: propertyCount, StringComparer.OrdinalIgnoreCase);
        
        system.Add(WinEventExtensions.EventIdKey, record.Id.ToString());
        system.Add(WinEventExtensions.ChannelKey, channel);
        system.Add(WinEventExtensions.ProviderNameKey, record.ProviderName);
        system.Add(WinEventExtensions.ProviderGuidKey, record.ProviderId.ToString());
        system.Add(WinEventExtensions.SystemTimeKey, record.Timestamp.ToUniversalTime().ToString("O"));
        system.Add(WinEventExtensions.ComputerKey, MachineNameHelper.FullyQualifiedName);
        system.Add(WinEventExtensions.ProcessIdKey, record.ProcessId.ToString());
        system.Add(WinEventExtensions.ThreadIdKey, record.ThreadId.ToString());
        
        return new WinEvent(system, eventData);
    }
    
    public static WinEvent BuildWinEvent(this System.Diagnostics.Eventing.Reader.EventRecord record, string channel, int propertyCount, out Dictionary<string, string> eventData)
    {
        return BuildWinEvent(record, channel, propertyCount, server: MachineNameHelper.FullyQualifiedName, out eventData);
    }
    
    private static WinEvent BuildWinEvent(this System.Diagnostics.Eventing.Reader.EventRecord record, string channel, int propertyCount, string server, out Dictionary<string, string> eventData)
    {
        var system = new Dictionary<string, string>(capacity: 8, StringComparer.OrdinalIgnoreCase);
        eventData = new Dictionary<string, string>(capacity: propertyCount, StringComparer.OrdinalIgnoreCase);
        
        system.Add(WinEventExtensions.EventIdKey, record.Id.ToString());
        system.Add(WinEventExtensions.ChannelKey, channel);
        system.Add(WinEventExtensions.ProviderNameKey, record.ProviderName);
        system.Add(WinEventExtensions.ProviderGuidKey, record.ProviderId == null ? Guid.Empty.ToString() : record.ProviderId.Value.ToString());
        system.Add(WinEventExtensions.SystemTimeKey, record.TimeCreated == null ? DateTime.UtcNow.ToString("O") : record.TimeCreated.Value.ToUniversalTime().ToString("O"));
        system.Add(WinEventExtensions.ComputerKey, server);
        system.Add(WinEventExtensions.ProcessIdKey, !record.ProcessId.HasValue ? "0" : record.ProcessId.Value.ToString());
        system.Add(WinEventExtensions.ThreadIdKey, !record.ThreadId.HasValue ? "0" : record.ThreadId.Value.ToString());

        return new WinEvent(system, eventData);
    }
}