using Serilog.Core;
using Serilog.Events;

namespace Collector.Logging;

internal sealed class EnvironmentVariableLoggingLevelSwitch : LoggingLevelSwitch
{
    public EnvironmentVariableLoggingLevelSwitch(string environmentVariable)
    {
        if (Enum.TryParse(Environment.GetEnvironmentVariable(environmentVariable), ignoreCase: true, out LogEventLevel level))
        {
            MinimumLevel = level;
        }
    }
}