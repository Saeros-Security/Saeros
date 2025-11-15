using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Collector.Databases.Abstractions.Domain.Users;
using Collector.Databases.Abstractions.Extensions;
using Collector.Databases.Abstractions.Repositories.Dashboards;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Integrations;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Repositories.Users;
using Collector.Databases.Abstractions.Stores.Authentication;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Databases.Abstractions.Stores.Tracing;
using Collector.Services.Abstractions.Activity;
using Collector.Services.Abstractions.Databases;
using Collector.Services.Abstractions.Domains;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Implementation.Bridge.Dashboards.Extensions;
using Collector.Services.Implementation.Bridge.Helpers;
using DeviceId;
using DeviceId.Encoders;
using DeviceId.Formatters;
using Microsoft.Extensions.Primitives;
using QuikGraph;
using QuikGraph.Serialization;
using Shared.Helpers;
using Shared.Integrations;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Serialization;
using Shared.Streaming.Hubs;

namespace Collector.Hosting.Api;

internal static class ApiHandler
{
    #region No License Required

    public static Task Ping(HttpContext context)
    {
        var activityService = context.RequestServices.GetRequiredService<IActivityService>();
        activityService.SetActive();
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        return Task.CompletedTask;
    }

    private static string GetHardwareId()
    {
        var systemId = new DeviceIdBuilder()
            .OnWindows(windows => windows
                .AddProcessorId()
                .AddMotherboardSerialNumber()
                .AddSystemUuid())
            .UseFormatter(new HashDeviceIdFormatter(MD5.Create, new Base64UrlByteArrayEncoder()))
            .ToString();
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(systemId));
        return new Guid(hash).ToString();
    }

    public static async Task GetHardwareId(HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        await context.Response.WriteAsync(GetHardwareId(), context.RequestAborted);
    }

    public static async Task GetUsers(HttpContext context)
    {
        var userRepository = context.RequestServices.GetRequiredService<IUserRepository>();
        var users = JsonSerializer.Serialize(userRepository.GetUsers().Select(user => user.FromUserRecord()), SerializationContext.Default.IEnumerableUser);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        await context.Response.WriteAsync(users);
    }

    public static void CreateUser(HttpContext context)
    {
        var user = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.User)!;
        var userRepository = context.RequestServices.GetRequiredService<IUserRepository>();
        userRepository.CreateUser(user.FromUser());
        context.Response.StatusCode = (int)HttpStatusCode.NoContent;
    }

    public static async Task UpdateLicense(HttpContext context)
    {
        var updateLicense = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.UpdateLicense)!;
        LicenseHelper.LoadLicense(updateLicense.License);
        if (LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature(), out var expiresAt))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync($"The license expires on {expiresAt:D}");
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        }
    }

    public static void HasAnyLicense(HttpContext context)
    {
        if (!LicenseHelper.HasAnyLicense())
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
    }
    
    public static async Task HasAnyDomain(HttpContext context)
    {
        var settingsRepository = context.RequestServices.GetRequiredService<ISettingsRepository>();
        try
        {
            var anyDomain = false;
            await foreach (var _ in settingsRepository.EnumerateDomainsAsync(context.RequestAborted))
            {
                anyDomain = true;
                break;
            }

            if (anyDomain)
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            }
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    #endregion

    public static void IsLicenseValid(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.NoContent;
    }

    public static void ChangeUserPassword(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var changeUserPassword = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.ChangeUserPassword)!;
        var userRepository = context.RequestServices.GetRequiredService<IUserRepository>();
        if (LicenseHelper.MatchActive(changeUserPassword.License) && userRepository.TryGetUserByName(changeUserPassword.Username, out var user))
        {
            userRepository.ChangeUserPassword(new UserRecord(user.Id, user.Username, Sha1Helper.Hash(changeUserPassword.NewPassword)));
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        }
    }

    public static void ValidateLicense(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var validateLicense = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.ValidateLicense)!;
        if (LicenseHelper.MatchActive(validateLicense.License))
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        }
    }

    public static async Task Login(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var user = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.User)!;
        var userRepository = context.RequestServices.GetRequiredService<IUserRepository>();
        try
        {
            if (userRepository.TryGetUserByName(user.Username, out var userRecord))
            {
                if (userRecord.PasswordHash.Equals(Sha1Helper.Hash(user.Password)))
                {
                    var authenticationStore = context.RequestServices.GetRequiredService<IAuthenticationStore>();
                    var authorizationValue = await authenticationStore.CreateAuthorizationValueAsync(userRecord.Username, context.RequestAborted);
                    context.Response.Headers.Authorization = new StringValues($"Bearer {authorizationValue}");
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task GetComputers(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            var computers = await detectionRepository.GetComputersAsync(context.RequestAborted);
            var serialization = JsonSerializer.Serialize(computers, SerializationContext.Default.IEnumerableComputer);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetRuleMetrics(HttpContext context, string ruleId)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }
            
        var dashboardRepository = context.RequestServices.GetRequiredService<IDashboardRepository>();
        try
        {
            var ruleMetrics = await dashboardRepository.GetRuleMetrics(ruleId, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(ruleMetrics, SerializationContext.Default.IEnumerableRuleMetrics);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetHomeMetrics(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }
            
        var dashboardRepository = context.RequestServices.GetRequiredService<IDashboardRepository>();
        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            var metrics = await dashboardRepository.GetHomeMetricsAsync(context.RequestAborted);
            var activeRules = await detectionRepository.GetActiveRulesAsync(context.RequestAborted);
            var consolidatedDashboard = metrics.OrderBy(metric => metric.Date).TakeLast(Core.Constants.MaxRetentionDays).ToList().Consolidate(Core.Constants.MaxRetentionDays, activeRules.Count());
            var serialization = JsonSerializer.Serialize(consolidatedDashboard, SerializationContext.Default.HomeMetrics);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task GetDetections(HttpContext context, int paginationLimit, long beforeId, long beforeDate)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionQuery = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.DetectionQuery);
        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            var detections = await detectionRepository.GetAsync(paginationLimit, beforeId, beforeDate, detectionQuery, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(detections, SerializationContext.Default.Detections);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetWinEvent(HttpContext context, long detectionId)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            var winEvent = await detectionRepository.GetWinEventAsync(detectionId, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(winEvent, SerializationContext.Default.WinEvent);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task GetDetectionFilters(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        var ruleRepository = context.RequestServices.GetRequiredService<IRuleRepository>();
        try
        {
            var computersTask = detectionRepository.GetComputersAsync(context.RequestAborted);
            var ruleTitlesTask = ruleRepository.GetRuleTitlesAsync(context.RequestAborted);
            await Task.WhenAll(computersTask, ruleTitlesTask);
            var detectionFilters = new DetectionFilters(computersTask.Result, ruleTitlesTask.Result, ruleRepository.GetMitres());
            var serialization = JsonSerializer.Serialize(detectionFilters, SerializationContext.Default.DetectionFilters);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task GetRules(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleRepository = context.RequestServices.GetRequiredService<IRuleRepository>();
        try
        {
            var rules = await ruleRepository.GetAsync(context.RequestAborted);
            var serialization = JsonSerializer.Serialize(rules, SerializationContext.Default.Rules);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetRule(HttpContext context, string ruleId)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleRepository = context.RequestServices.GetRequiredService<IRuleRepository>();
        try
        {
            var rule = await ruleRepository.GetAsync(ruleId, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(rule, SerializationContext.Default.Rule);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetRuleByTitle(HttpContext context, string ruleTitle)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleRepository = context.RequestServices.GetRequiredService<IRuleRepository>();
        try
        {
            var rule = await ruleRepository.GetByTitleAsync(ruleTitle, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(rule, SerializationContext.Default.Rule);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetDetection(HttpContext context, long id)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            var detection = await detectionRepository.GetAsync(id, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(detection, SerializationContext.Default.Detection);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task CreateRule(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        var settingsStore = context.RequestServices.GetRequiredService<ISettingsStore>();
        try
        {
            var createRule = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.CreateRule);
            var createdRule = await ruleService.CreateRuleAsync(createRule!, settingsStore.OverrideAuditPolicies ? RuleHub.AuditPolicyPreference.Override : RuleHub.AuditPolicyPreference.NotOverride, channelForwarding: true, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(createdRule, SerializationContext.Default.CreatedRule);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
        catch (Exception ex)
        {
            var createdRule = new CreatedRule(title: string.Empty, string.Empty, error: ex.Message);
            var serialization = JsonSerializer.Serialize(createdRule, SerializationContext.Default.CreatedRule);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
    }
    
    public static async Task CopyRule(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        try
        {
            var copyRule = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.CopyRule);
            var createdRule = await ruleService.CopyRuleAsync(copyRule!, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(createdRule, SerializationContext.Default.CreatedRule);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
        catch (Exception ex)
        {
            var createdRule = new CreatedRule(title: string.Empty, string.Empty, error: ex.Message);
            var serialization = JsonSerializer.Serialize(createdRule, SerializationContext.Default.CreatedRule);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
    }
    
    public static async Task EnableRule(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        try
        {
            var enableRule = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.EnableRule);
            await ruleService.EnableRulesAsync([enableRule!], context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task DisableRule(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        try
        {
            var disableRule = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.DisableRule);
            await ruleService.DisableRulesAsync([disableRule!], commit: true, context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task EnableRules(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        try
        {
            var enableRules = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.EnableRules);
            await ruleService.EnableRulesAsync(enableRules!.RuleIds.Select(ruleId => new EnableRule(ruleId)).ToList(), context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task DisableRules(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        try
        {
            var disableRules = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.DisableRules);
            await ruleService.DisableRulesAsync(disableRules!.RuleIds.Select(ruleId => new DisableRule(ruleId)).ToList(), commit: true, context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task DeleteRule(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        try
        {
            var deleteRule = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.DeleteRule);
            await ruleService.DisableRulesAsync([new DisableRule(deleteRule!.RuleId)], commit: true, context.RequestAborted);
            await ruleService.DeleteRulesAsync([deleteRule], context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task DeleteRules(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        try
        {
            var deleteRules = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.DeleteRules);
            await ruleService.DisableRulesAsync(deleteRules!.RuleIds.Select(ruleId => new DisableRule(ruleId)).ToList(), commit: true, context.RequestAborted);
            await ruleService.DeleteRulesAsync(deleteRules.RuleIds.Select(ruleId => new DeleteRule(ruleId)).ToList(), context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task UpdateRuleCode(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
        try
        {
            var updateRuleCode = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.UpdateRuleCode);
            var response = await ruleService.UpdateRuleCodeAsync(updateRuleCode!, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(response, SerializationContext.Default.UpdatedRule);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task ExportRuleCode(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleRepository = context.RequestServices.GetRequiredService<IRuleRepository>();
        try
        {
            var exportRuleCode = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.ExportRuleCode);
            var response = await ruleRepository.GetRuleContentAsync(exportRuleCode!.RuleId, context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(response ?? string.Empty);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task GetRuleAttributes(HttpContext context, string ruleId)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var ruleRepository = context.RequestServices.GetRequiredService<IRuleRepository>();
        try
        {
            var attributes = await ruleRepository.GetAttributesAsync(ruleId, context.RequestAborted);
            var serialization = JsonSerializer.Serialize(attributes, SerializationContext.Default.RuleAttributes);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetTimeline(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            var getTimeline = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.GetTimeline);
            var timeline = await detectionRepository.GetTimelineAsync(getTimeline!.Query, getTimeline.Step,  context.RequestAborted);
            var serialization = JsonSerializer.Serialize(timeline, SerializationContext.Default.Timeline);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetMitre(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            var mitresByMitreId = detectionRepository.GetMitresByMitreId();
            var serialization = JsonSerializer.Serialize(mitresByMitreId, SerializationContext.Default.MitresByMitreId);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task Exclude(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            var createRuleExclusion = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.CreateRuleExclusion);
            await detectionRepository.Exclude(createRuleExclusion!, context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetExclusions(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        var ruleRepository = context.RequestServices.GetRequiredService<IRuleRepository>();
        try
        {
            var exclusions = await detectionRepository.GetExclusions(ruleId => ruleRepository.GetAsync(ruleId, context.RequestAborted), context.RequestAborted);
            var serialization = JsonSerializer.Serialize(exclusions, SerializationContext.Default.IEnumerableExclusion);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetSuccessLogons(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        try
        {
            var tracingStore = context.RequestServices.GetRequiredService<ITracingStore>();
            var serialization = JsonSerializer.Serialize(tracingStore.EnumerateSuccessLogons().Select(logon => new SuccessLogon(logon.Count, logon.TargetAccount, string.Join(", ", logon.TargetComputer), string.Join(", ", logon.LogonType), string.Join(", ", logon.SourceComputer), string.Join(", ", logon.SourceIpAddress))), SerializationContext.Default.IEnumerableSuccessLogon);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetFailureLogons(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        try
        {
            var tracingStore = context.RequestServices.GetRequiredService<ITracingStore>();
            var serialization = JsonSerializer.Serialize(tracingStore.EnumerateFailureLogons().Select(logon => new FailureLogon(logon.Count, logon.TargetAccount, string.Join(", ", logon.TargetComputer), string.Join(", ", logon.LogonType), string.Join(", ", logon.SourceComputer), string.Join(", ", logon.SourceIpAddress))), SerializationContext.Default.IEnumerableFailureLogon);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task DeleteExclusion(HttpContext context, int id)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var detectionRepository = context.RequestServices.GetRequiredService<IDetectionRepository>();
        try
        {
            await detectionRepository.DeleteExclusion(id, context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task UpdateIntegration(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var integrationRepository = context.RequestServices.GetRequiredService<IIntegrationRepository>();
        try
        {
            var updateIntegration = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.UpdateIntegration);
            await integrationRepository.UpdateIntegrationAsync(updateIntegration!, context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetIntegrations(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var integrationRepository = context.RequestServices.GetRequiredService<IIntegrationRepository>();
        try
        {
            var integrations = await integrationRepository.GetIntegrationsAsync(context.RequestAborted);
            var serialization = JsonSerializer.Serialize(integrations, IntegrationJsonExtensions.Options);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task GetSettings(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        try
        {
            var settingsRepository = context.RequestServices.GetRequiredService<ISettingsRepository>();
            var serialization = JsonSerializer.Serialize(await settingsRepository.GetSettingsAsync(context.RequestAborted), SerializationContext.Default.Settings);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(serialization);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task SetProfile(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        try
        {
            var setProfile = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.SetProfile)!;
            var settingsRepository = context.RequestServices.GetRequiredService<ISettingsRepository>();
            var profileChange = await settingsRepository.SetProfileAsync(setProfile.Profile, context.RequestAborted);
            if (profileChange.Changed)
            {
                var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
                await ruleService.UpdateAsync(includeNonBuiltinRules: false, context.RequestAborted);
            }
            
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task SetSettings(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        try
        {
            var settingsRepository = context.RequestServices.GetRequiredService<ISettingsRepository>();
            var settings = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.SetSettings)!;
            var settingsChange = await settingsRepository.SetSettingsAsync(settings, context.RequestAborted);
            if (settingsChange.OverrideAuditPoliciesChanged)
            {
                var ruleService = context.RequestServices.GetRequiredService<IRuleService>();
                await ruleService.UpdateAsync(includeNonBuiltinRules: false, context.RequestAborted);
            }
            
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task GetTracing(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var tracingStore = context.RequestServices.GetRequiredService<ITracingStore>();
        try
        {
            var query = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.TracingQuery);
            var graph = await tracingStore.TrySerializeGraph(query!, context.RequestAborted);
            if (graph is { IsEdgesEmpty: true, IsVerticesEmpty: true })
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var result = Results.Stream(async stream =>
            {
                await using var writer = XmlWriter.Create(stream, new XmlWriterSettings
                {
                    Async = true,
                    CloseOutput = false
                });

                graph.SerializeToGraphML<TracingNode, IEdge<TracingNode>, IEdgeListGraph<TracingNode, IEdge<TracingNode>>>(writer, vertex => vertex.Id, edge => $"{edge.Source.Id}->{edge.Target.Id}");
            });

            await result.ExecuteAsync(context);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task ExportDatabase(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var databaseExporterService = context.RequestServices.GetRequiredService<IDatabaseExporterService>();
        try
        {
            var export = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.ExportDatabase);
            await databaseExporterService.ExportTablesAsync(export!.Path, context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
    
    public static async Task ImportDatabase(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var databaseExporterService = context.RequestServices.GetRequiredService<IDatabaseExporterService>();
        try
        {
            var import = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.ImportDatabase);
            await databaseExporterService.ImportTablesAsync(import!.Path, context.RequestAborted);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task AddOrUpdateDomain(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var domainService = context.RequestServices.GetRequiredService<IDomainService>();
        try
        {
            var updateDomain = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.UpdateDomain)!;
            try
            {
                await domainService.AddOrUpdateAsync(updateDomain.Username, updateDomain.Password, updateDomain.Name, updateDomain.PrimaryDomainController, updateDomain.LdapPort, context.RequestAborted);
                var serialization = JsonSerializer.Serialize(new DomainResponse(success: true), SerializationContext.Default.DomainResponse);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await context.Response.WriteAsync(serialization);
            }
            catch (Exception ex)
            {
                var serialization = JsonSerializer.Serialize(new DomainResponse(success: false, error: ex.Message), SerializationContext.Default.DomainResponse);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await context.Response.WriteAsync(serialization);
            }
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }

    public static async Task DeleteDomain(HttpContext context)
    {
        if (!LicenseHelper.IsLicenseValid(ProductFeatureHelper.GetFeature()))
        {
            context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
            return;
        }

        var domainService = context.RequestServices.GetRequiredService<IDomainService>();
        try
        {
            var deleteDomain = JsonSerializer.Deserialize(context.Request.Body, SerializationContext.Default.DeleteDomain)!;
            try
            {
                await domainService.DeleteAsync(deleteDomain.Username, deleteDomain.Password, deleteDomain.Name, deleteDomain.PrimaryDomainController, deleteDomain.LdapPort, context.RequestAborted);
                var serialization = JsonSerializer.Serialize(new DomainResponse(success: true), SerializationContext.Default.DomainResponse);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await context.Response.WriteAsync(serialization);
            }
            catch (Exception ex)
            {
                var serialization = JsonSerializer.Serialize(new DomainResponse(success: false, error: ex.Message), SerializationContext.Default.DomainResponse);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await context.Response.WriteAsync(serialization);
            }
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
        }
    }
}