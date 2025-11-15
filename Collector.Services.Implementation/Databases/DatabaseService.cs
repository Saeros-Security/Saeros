using System.Diagnostics;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Implementation.Caching.Series;
using Collector.Databases.Implementation.Contexts.AuditPolicies;
using Collector.Databases.Implementation.Contexts.Dashboards;
using Collector.Databases.Implementation.Contexts.Detections;
using Collector.Databases.Implementation.Contexts.Integrations;
using Collector.Databases.Implementation.Contexts.RuleConfigurations;
using Collector.Databases.Implementation.Contexts.Rules;
using Collector.Databases.Implementation.Contexts.Settings;
using Collector.Databases.Implementation.Contexts.Tracing;
using Collector.Databases.Implementation.Contexts.Users;
using Collector.Services.Abstractions.Databases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Databases.Collector.Context.Licenses;

namespace Collector.Services.Implementation.Databases;

public sealed class DatabaseService(ILogger<DatabaseService> logger, IServiceProvider serviceProvider) : IDatabaseService
{
    public async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var ruleContext = serviceProvider.GetService<RuleContext>();
        ruleContext?.CreateTables();
        
        if (ruleContext is not null)
        {
            var ruleRepository = serviceProvider.GetService<IRuleRepository>();
            if (ruleRepository is not null)
            {
                await ruleRepository.InitializeAsync(cancellationToken);
            }
        }
        
        var ruleConfigurationContext = serviceProvider.GetService<RuleConfigurationContext>();
        ruleConfigurationContext?.CreateTables();
        
        var auditPoliciesContext = serviceProvider.GetService<AuditPoliciesContext>();
        auditPoliciesContext?.CreateTables();
        
        var userContext = serviceProvider.GetService<UserContext>();
        userContext?.CreateTables();
        
        var licenseContext = serviceProvider.GetService<CollectorLicenseContext>();
        licenseContext?.CreateTables();
        
        var dashboardContext = serviceProvider.GetService<DashboardContext>();
        dashboardContext?.CreateTables();
        
        var integrationContext = serviceProvider.GetService<IntegrationContext>();
        integrationContext?.CreateTables();
        
        var settingsContext = serviceProvider.GetService<SettingsContext>();
        settingsContext?.CreateTables();
        
        var tracingContext = serviceProvider.GetService<TracingContext>();
        tracingContext?.CreateTables();

        if (tracingContext is not null)
        {
            var tracingRepository = serviceProvider.GetService<ITracingRepository>();
            if (tracingRepository is not null)
            {
                await tracingRepository.InitializeAsync(cancellationToken);
            }
        }
        
        if (settingsContext is not null)
        {
            var settingsRepository = serviceProvider.GetService<ISettingsRepository>();
            if (settingsRepository is not null)
            {
                await settingsRepository.InitializeAsync(cancellationToken);
            }
        }
        
        var detectionContext = serviceProvider.GetService<DetectionContext>();
        detectionContext?.CreateTables();

        if (detectionContext is not null)
        {
            var detectionRepository = serviceProvider.GetService<IDetectionRepository>();
            if (detectionRepository is not null)
            {
                await detectionRepository.InitializeAsync(cancellationToken);
            }
        }
        
        var eventSeries = serviceProvider.GetService<EventSeries>();
        if (eventSeries is not null)
        {
            await eventSeries.InitializeAsync(cancellationToken);
        }
        
        var networkSeries = serviceProvider.GetService<NetworkSeries>();
        if (networkSeries is not null)
        {
            await networkSeries.InitializeAsync(cancellationToken);
        }
        
        var tracingSeries = serviceProvider.GetService<TracingSeries>();
        if (tracingSeries is not null)
        {
            await tracingSeries.InitializeAsync(cancellationToken);
        }
        
        logger.LogInformation("Databases loaded in '{Time}s'", stopwatch.Elapsed.TotalSeconds);
    }
}