using Collector.Extensions;
using Collector.Hosting.Api;
using Collector.Hosting.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Shared.Streaming.Hubs;

namespace Collector.Hosting;

internal sealed class BridgeStartup
{
    public void Configure(IApplicationBuilder app, IConfiguration configuration, ILogger<BridgeStartup> logger)
    {
        app.Use((context, next) =>
        {
            context.Request.Scheme = "https";
            return next(context);
        });
        
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseRequestLogging();
        app.UseRouting();
        app.UseMiddleware<AuthorizationMiddleware>();
        app.UseGrpcWeb();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<SystemAuditHub>().EnableGrpcWeb();
            endpoints.MapGrpcService<LicenseHub>().EnableGrpcWeb();
            endpoints.MapGrpcService<DashboardHub>().EnableGrpcWeb();
            endpoints.MapGrpcService<DetectionHub>().EnableGrpcWeb();
            endpoints.MapGrpcService<RuleHub>().EnableGrpcWeb();
            endpoints.MapGrpcService<EventHub>().EnableGrpcWeb();

            endpoints.MapGet(Shared.Constants.Routes.Ping, ApiHandler.Ping);
            endpoints.MapGet(Shared.Constants.Routes.HardwareId, ApiHandler.GetHardwareId);
            endpoints.MapPost(Shared.Constants.Routes.Login, ApiHandler.Login);

            endpoints.MapGet(Shared.Constants.Routes.Users, ApiHandler.GetUsers);
            endpoints.MapPost(Shared.Constants.Routes.CreateUser, ApiHandler.CreateUser);
            endpoints.MapPost(Shared.Constants.Routes.ChangeUserPassword, ApiHandler.ChangeUserPassword);

            endpoints.MapGet(Shared.Constants.Routes.License, ApiHandler.IsLicenseValid);
            endpoints.MapGet(Shared.Constants.Routes.AnyLicense, ApiHandler.HasAnyLicense);
            endpoints.MapPost(Shared.Constants.Routes.ValidateLicense, ApiHandler.ValidateLicense);
            endpoints.MapPost(Shared.Constants.Routes.UpdateLicense, ApiHandler.UpdateLicense);

            endpoints.MapGet(Shared.Constants.Routes.HomeMetrics, ApiHandler.GetHomeMetrics);
            endpoints.MapGet(Shared.Constants.Routes.RuleMetrics, (HttpContext context, [FromQuery(Name = "ruleId")] string ruleId) => ApiHandler.GetRuleMetrics(context, ruleId));

            endpoints.MapGet(Shared.Constants.Routes.Computers, ApiHandler.GetComputers);

            endpoints.MapGet(Shared.Constants.Routes.WinEvent, (HttpContext context, [FromQuery(Name = "detectionId")] long detectionId) => ApiHandler.GetWinEvent(context, detectionId));

            endpoints.MapGet(Shared.Constants.Routes.Detection, (HttpContext context, [FromQuery(Name = "id")] long id) => ApiHandler.GetDetection(context, id));
            endpoints.MapPost(Shared.Constants.Routes.Detections, (HttpContext context, [FromQuery(Name = "limit")] int limit, [FromQuery(Name = "beforeId")] long beforeId, [FromQuery(Name = "beforeDate")] long beforeDate) => ApiHandler.GetDetections(context, limit, beforeId, beforeDate));
            endpoints.MapGet(Shared.Constants.Routes.DetectionFilters, ApiHandler.GetDetectionFilters);

            endpoints.MapGet(Shared.Constants.Routes.Rules, ApiHandler.GetRules);
            endpoints.MapGet(Shared.Constants.Routes.Rule, (HttpContext context, [FromQuery(Name = "ruleId")] string ruleId) => ApiHandler.GetRule(context, ruleId));
            endpoints.MapGet(Shared.Constants.Routes.RuleAttributes, (HttpContext context, [FromQuery(Name = "ruleId")] string ruleId) => ApiHandler.GetRuleAttributes(context, ruleId));
            endpoints.MapGet(Shared.Constants.Routes.RuleTitle, (HttpContext context, [FromRoute(Name = "title")] string title) => ApiHandler.GetRuleByTitle(context, title));
            endpoints.MapPost(Shared.Constants.Routes.CreateRule, ApiHandler.CreateRule);
            endpoints.MapPost(Shared.Constants.Routes.CopyRule, ApiHandler.CopyRule);
            endpoints.MapPost(Shared.Constants.Routes.EnableRule, ApiHandler.EnableRule);
            endpoints.MapPost(Shared.Constants.Routes.DisableRule, ApiHandler.DisableRule);
            endpoints.MapPost(Shared.Constants.Routes.EnableRules, ApiHandler.EnableRules);
            endpoints.MapPost(Shared.Constants.Routes.DisableRules, ApiHandler.DisableRules);
            endpoints.MapPost(Shared.Constants.Routes.DeleteRule, ApiHandler.DeleteRule);
            endpoints.MapPost(Shared.Constants.Routes.DeleteRules, ApiHandler.DeleteRules);
            endpoints.MapPost(Shared.Constants.Routes.UpdateRuleCode, ApiHandler.UpdateRuleCode);
            endpoints.MapPost(Shared.Constants.Routes.ExportRuleCode, ApiHandler.ExportRuleCode);
            endpoints.MapPost(Shared.Constants.Routes.Timeline, ApiHandler.GetTimeline);
            endpoints.MapGet(Shared.Constants.Routes.Mitre, ApiHandler.GetMitre);
            endpoints.MapPost(Shared.Constants.Routes.RuleExclusion, ApiHandler.Exclude);
            endpoints.MapGet(Shared.Constants.Routes.Exclusion, ApiHandler.GetExclusions);
            endpoints.MapDelete(Shared.Constants.Routes.Exclusion, (HttpContext context, [FromQuery(Name = "id")] int id) => ApiHandler.DeleteExclusion(context, id));

            endpoints.MapGet(Shared.Constants.Routes.SuccessLogon, ApiHandler.GetSuccessLogons);
            endpoints.MapGet(Shared.Constants.Routes.FailedLogon, ApiHandler.GetFailureLogons);

            endpoints.MapGet(Shared.Constants.Routes.Integration, ApiHandler.GetIntegrations);
            endpoints.MapPut(Shared.Constants.Routes.Integration, ApiHandler.UpdateIntegration);

            endpoints.MapGet(Shared.Constants.Routes.Settings, ApiHandler.GetSettings);
            endpoints.MapPost(Shared.Constants.Routes.Profile, ApiHandler.SetProfile);
            endpoints.MapPost(Shared.Constants.Routes.Settings, ApiHandler.SetSettings);
            endpoints.MapPost(Shared.Constants.Routes.Export, ApiHandler.ExportDatabase);
            endpoints.MapPost(Shared.Constants.Routes.Import, ApiHandler.ImportDatabase);

            endpoints.MapPost(Shared.Constants.Routes.Tracing, ApiHandler.GetTracing);

            endpoints.MapPost(Shared.Constants.Routes.Domain, ApiHandler.AddOrUpdateDomain);
            endpoints.MapDelete(Shared.Constants.Routes.Domain, ApiHandler.DeleteDomain);
            endpoints.MapGet(Shared.Constants.Routes.AnyDomain, ApiHandler.HasAnyDomain);
        });
    }
}