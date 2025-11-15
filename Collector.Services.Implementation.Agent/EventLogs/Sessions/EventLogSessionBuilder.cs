using Collector.Services.Abstractions.EventProviders.Registries;
using Collector.Services.Implementation.Agent.EventLogs.Pipelines;
using Microsoft.Extensions.Logging;
using Microsoft.O365.Security.ETW;

namespace Collector.Services.Implementation.Agent.EventLogs.Sessions;

public class EventLogSessionBuilder
{
    private readonly IDictionary<string, EventLogSession> _sessionByChannelName = new Dictionary<string, EventLogSession>();
    private readonly ILogger _logger;

    private EventLogSessionBuilder(ILogger logger)
    {
        _logger = logger;
    }

    public EventLogSessionBuilder WithUserTrace(Action<EventTraceProperties> configure, string name, string? channelName)
    {
        return WithTrace(configure, name, channelName ?? EventProviderRegistry.UserSession, traceFactory: n => new UserTrace(n), SessionType.User);
    }
    
    public EventLogSessionBuilder WithKernelTrace(Action<EventTraceProperties> configure, string name, string? channelName)
    {
        return WithTrace(configure, name, channelName ?? EventProviderRegistry.KernelSession, traceFactory: n => new KernelTrace(n), SessionType.Kernel);
    }

    private EventLogSessionBuilder WithTrace(Action<EventTraceProperties> configure, string name, string channelName, Func<string, ITrace> traceFactory, SessionType sessionType)
    {
        if(string.IsNullOrWhiteSpace(name)) throw new Exception("Name cannot be null or empty");
        var properties = new EventTraceProperties();
        configure(properties);
        _sessionByChannelName.Add(channelName, new EventLogSession(name, channelName, new Lazy<ITrace>(() =>
        {
            var trace = traceFactory(name);
            trace.SetTraceProperties(properties);
            return trace;
        }), new WinEventLogPipeline(_logger), sessionType));
        
        return this;
    }

    public IDictionary<string, EventLogSession> Build()
    {
        return _sessionByChannelName;
    }
    
    public static EventLogSessionBuilder Create(ILogger logger)
    {
        return new EventLogSessionBuilder(logger);
    }
}