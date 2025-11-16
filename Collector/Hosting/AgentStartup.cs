using Collector.Extensions;
using Collector.Hosting.Middlewares;
using Shared.Streaming.Hubs;

namespace Collector.Hosting;

internal sealed class AgentStartup
{
    public void Configure(IApplicationBuilder app, IConfiguration configuration, ILogger<AgentStartup> logger)
    {
        app.Use((context, next) =>
        {
            context.Request.Scheme = "https";
            return next(context);
        });
        
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseRequestLogging();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<DetectionHub>();
            endpoints.MapGrpcService<RuleHub>();
            endpoints.MapGrpcService<ProcessTreeHub>();
            endpoints.MapGrpcService<TracingHub>();
            endpoints.MapGrpcService<EventHub>();
            endpoints.MapGrpcService<SystemAuditHub>();
            endpoints.MapGrpcService<MetricHub>();
        });
    }
}
