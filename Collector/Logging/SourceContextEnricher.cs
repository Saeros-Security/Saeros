using Serilog.Core;
using Serilog.Events;

namespace Collector.Logging;

internal sealed class SourceContextEnricher : ILogEventEnricher
{
    private const string PropertyName = "SourceContext";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.TryGetValue(PropertyName, out var eventPropertyValue))
        {
            var value = (eventPropertyValue as ScalarValue)?.Value as string;
            if (!string.IsNullOrEmpty(value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(PropertyName, new ScalarValue(value.Split(".").Last())));
            }
        }
    }
}