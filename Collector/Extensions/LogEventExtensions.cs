using Serilog.Events;

namespace Collector.Extensions;

internal static class LogEventExtensions
{
    public static bool Filter(this LogEvent logEvent)
    {
        if (!logEvent.Properties.ContainsKey("GrpcUri")) return false;
        return logEvent.Properties.TryGetValue("StatusCode", out _);
    }
}