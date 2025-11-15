using Collector.Services.Abstractions.EventProviders.Registries;
using Collector.Services.Implementation.Agent.EventLogs.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.O365.Security.ETW;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Helpers;

internal static class ManifestEventLogSessions
{
    public static IDictionary<string, EventLogSession> BuildSessions(ILogger logger)
    {
        var builder = EventLogSessionBuilder.Create(logger)
            .WithUserTrace(configure: settings =>
            {
                settings.BufferSize = 64;
                settings.MinimumBuffers = Convert.ToUInt32(2 * Environment.ProcessorCount);
                settings.FlushTimer = 5;
                settings.LogFileMode = (uint)LogFileModeFlags.FLAG_EVENT_TRACE_USE_PAGED_MEMORY | (uint)LogFileModeFlags.FLAG_EVENT_TRACE_INDEPENDENT_SESSION_MODE | (uint)LogFileModeFlags.FLAG_EVENT_TRACE_REAL_TIME_MODE;
            }, name: "EventLog-Security", channelName: "Security")
            .WithUserTrace(configure: settings =>
            {
                settings.BufferSize = 64;
                settings.MinimumBuffers = Convert.ToUInt32(2 * Environment.ProcessorCount);
                settings.FlushTimer = 5;
                settings.LogFileMode = (uint)LogFileModeFlags.FLAG_EVENT_TRACE_USE_PAGED_MEMORY | (uint)LogFileModeFlags.FLAG_EVENT_TRACE_INDEPENDENT_SESSION_MODE | (uint)LogFileModeFlags.FLAG_EVENT_TRACE_REAL_TIME_MODE;
            }, name: "EventLog-System", channelName: "System")
            .WithUserTrace(configure: settings =>
            {
                settings.BufferSize = 64;
                settings.MinimumBuffers = Convert.ToUInt32(2 * Environment.ProcessorCount);
                settings.FlushTimer = 5;
                settings.LogFileMode = (uint)LogFileModeFlags.FLAG_EVENT_TRACE_USE_PAGED_MEMORY | (uint)LogFileModeFlags.FLAG_EVENT_TRACE_INDEPENDENT_SESSION_MODE | (uint)LogFileModeFlags.FLAG_EVENT_TRACE_REAL_TIME_MODE;
            }, name: "EventLog-Application", channelName: "Application")
            .WithUserTrace(configure: settings =>
            {
                settings.BufferSize = 64;
                settings.MinimumBuffers = Convert.ToUInt32(2 * Environment.ProcessorCount);
                settings.FlushTimer = 5;
                settings.LogFileMode = (uint)LogFileModeFlags.FLAG_EVENT_TRACE_USE_PAGED_MEMORY | (uint)LogFileModeFlags.FLAG_EVENT_TRACE_INDEPENDENT_SESSION_MODE | (uint)LogFileModeFlags.FLAG_EVENT_TRACE_REAL_TIME_MODE;
            }, name: EventProviderRegistry.UserTrace, channelName: null)
            .WithKernelTrace(configure: settings =>
            {
                settings.BufferSize = 64;
                settings.MinimumBuffers = Convert.ToUInt32(2 * Environment.ProcessorCount);
                settings.FlushTimer = 5;
            }, name: EventProviderRegistry.KernelTrace, channelName: null);

        return builder.Build();
    }
}