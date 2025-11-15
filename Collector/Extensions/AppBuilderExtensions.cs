using Serilog;
using Serilog.Events;
using Streaming;

namespace Collector.Extensions;

public static class AppBuilderExtensions
{
    public static void UseRequestLogging(this IApplicationBuilder builder)
    {
        builder.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
        {
            if (context.Response.StatusCode >= 499) return LogEventLevel.Error;
            if (exception is not null) return LogEventLevel.Error;
            if (context.Request.Path.Equals(new PathString(Shared.Constants.Routes.Ping))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(DetectionRpcService)}"))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(DashboardRpcService)}"))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(LicenseRpcService)}"))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(SystemAuditRpcService)}"))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(EventRpcService)}"))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(RuleRpcService)}"))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(ProcessRpcService)}"))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(MetricRpcService)}"))) return LogEventLevel.Debug;
            if (context.Request.Path.StartsWithSegments(new PathString($"/streaming.{nameof(TracingRpcService)}"))) return LogEventLevel.Debug;
            return LogEventLevel.Information;
        });
    }
}