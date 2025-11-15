using Autofac;
using Collector.Core.HostedServices.GarbageCollection;
using Collector.Core.Hubs.Dashboards;
using Collector.Core.Hubs.Detections;
using Collector.Core.Hubs.Events;
using Collector.Core.Hubs.Licenses;
using Collector.Core.Hubs.Rules;
using Collector.Core.Hubs.SystemAudits;
using Collector.Core.Modules;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Repositories.AuditPolicies;
using Collector.Databases.Abstractions.Repositories.Dashboards;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Integrations;
using Collector.Databases.Abstractions.Repositories.RuleConfigurations;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Repositories.Settings;
using Collector.Databases.Abstractions.Repositories.Tracing;
using Collector.Databases.Abstractions.Repositories.Users;
using Collector.Databases.Abstractions.Stores.Detections;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Abstractions.Stores.Rules;
using Collector.Databases.Abstractions.Stores.Settings;
using Collector.Databases.Abstractions.Stores.Tracing;
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
using Collector.Databases.Implementation.Repositories.AuditPolicies;
using Collector.Databases.Implementation.Repositories.Dashboards;
using Collector.Databases.Implementation.Repositories.Detections;
using Collector.Databases.Implementation.Repositories.Integrations;
using Collector.Databases.Implementation.Repositories.RuleConfigurations;
using Collector.Databases.Implementation.Repositories.Rules;
using Collector.Databases.Implementation.Repositories.Settings;
using Collector.Databases.Implementation.Repositories.Tracing;
using Collector.Databases.Implementation.Repositories.Users;
using Collector.Databases.Implementation.Stores.Detections;
using Collector.Databases.Implementation.Stores.Logon.Domain;
using Collector.Databases.Implementation.Stores.Logon.Machine;
using Collector.Databases.Implementation.Stores.Rules;
using Collector.Databases.Implementation.Stores.Settings;
using Collector.Databases.Implementation.Stores.Tracing;
using Collector.Integrations.Implementation;
using Collector.Services.Abstractions.Activity;
using Collector.Services.Abstractions.Dashboards;
using Collector.Services.Abstractions.Databases;
using Collector.Services.Abstractions.Domains;
using Collector.Services.Abstractions.Licenses;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Implementation.Bridge.Activity;
using Collector.Services.Implementation.Bridge.Dashboards;
using Collector.Services.Implementation.Bridge.Databases;
using Collector.Services.Implementation.Bridge.Domains;
using Collector.Services.Implementation.Bridge.HostedServices.Dashboards;
using Collector.Services.Implementation.Bridge.HostedServices.Domains;
using Collector.Services.Implementation.Bridge.HostedServices.Integrations;
using Collector.Services.Implementation.Bridge.HostedServices.Licenses;
using Collector.Services.Implementation.Bridge.HostedServices.SystemAudits;
using Collector.Services.Implementation.Bridge.HostedServices.Tracing;
using Collector.Services.Implementation.Bridge.Licenses;
using Collector.Services.Implementation.Bridge.NamedPipes;
using Collector.Services.Implementation.Bridge.Rules;
using Collector.Services.Implementation.Bridge.SystemAudits;
using Collector.Services.Implementation.Databases;
using Collector.Services.Implementation.Geolocation;
using Collector.Services.Implementation.HostedServices.Databases;
using Collector.Services.Implementation.HostedServices.Rules;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Databases.Collector;
using Shared.Databases.Collector.Context.Licenses;
using Shared.Databases.Collector.Repositories.Licences;
using Shared.Helpers;
using Shared.Streaming.Interfaces;

namespace Collector.Services.Implementation.Bridge;

public sealed class BridgeModule<THost>(Func<IServiceProvider, THost> hostFactory) : CoreModule
    where THost : IHostedService
{
    protected override void LoadDatabases(ContainerBuilder builder)
    {
        builder.RegisterType<RuleContext>().SingleInstance();
        builder.RegisterType<RuleConfigurationContext>().SingleInstance();
        builder.RegisterType<AuditPoliciesContext>().SingleInstance();
        builder.RegisterType<UserContext>().SingleInstance();
        builder.RegisterType<TracingContext>().SingleInstance();
        builder.RegisterType<CollectorLicenseContext>().SingleInstance();
        builder.RegisterType<DashboardContext>().SingleInstance();
        builder.RegisterType<DetectionContext>().SingleInstance();
        builder.RegisterType<IntegrationContext>().SingleInstance();
        builder.RegisterType<SettingsContext>().SingleInstance();
        builder.RegisterType<RuleRepository>().As<IRuleRepository>().SingleInstance();
        builder.RegisterType<RuleConfigurationsRepository>().As<IRuleConfigurationsRepository>().SingleInstance();
        builder.RegisterType<AuditPoliciesRepository>().As<IAuditPoliciesRepository>().SingleInstance();
        builder.RegisterType<UserRepository>().As<IUserRepository>().SingleInstance();
        builder.RegisterType<CollectorLicenseRepository>().As<ICollectorLicenseRepository>().SingleInstance();
        builder.RegisterType<DashboardRepository>().As<IDashboardRepository>().SingleInstance();
        builder.RegisterType<DetectionRepository>().As<IDetectionRepository>().SingleInstance();
        builder.RegisterType<IntegrationRepository>().As<IIntegrationRepository>().SingleInstance();
        builder.RegisterType<SettingsRepository>().As<ISettingsRepository>().SingleInstance();
        builder.RegisterType<TracingRepository>().As<ITracingRepository>().SingleInstance();
        builder.RegisterType<DetectionStore>().As<IDetectionStore>().SingleInstance();
        builder.RegisterType<SettingsStore>().As<ISettingsStore>().SingleInstance();
        builder.RegisterType<TracingStore>().As<ITracingStore>().SingleInstance();
        builder.RegisterType<RuleStore>().As<IRuleStore>().SingleInstance();
        builder.Register(x => new EventSeries(x.Resolve<ILogger<EventSeries>>(), CollectorContextBase.DbPath)).SingleInstance();
        builder.Register(x => new NetworkSeries(x.Resolve<ILogger<NetworkSeries>>(), CollectorContextBase.DbPath)).SingleInstance();
        builder.Register(x => new TracingSeries(x.Resolve<ILogger<TracingSeries>>(), CollectorContextBase.DbPath)).SingleInstance();
        if (DomainHelper.DomainJoined)
        {
            builder.RegisterType<DomainLogonStore>().As<ILogonStore>().SingleInstance();
        }
        else
        {
            builder.RegisterType<MachineLogonStore>().As<ILogonStore>().SingleInstance();
        }
    }

    protected override void LoadServices(ContainerBuilder builder)
    {
        builder.RegisterType<DatabaseService>().As<IDatabaseService>().SingleInstance();
        builder.RegisterType<DatabaseExporterService>().As<IDatabaseExporterService>().SingleInstance();
        builder.RegisterType<NamedPipeBridge>().As<INamedPipeBridge>().SingleInstance();
        builder.RegisterType<RuleServiceBridge>().As<IRuleService>().SingleInstance();
        builder.RegisterType<LicenseService>().As<ILicenseService>().SingleInstance();
        builder.RegisterType<GeolocationService>().As<IGeolocationService>().SingleInstance();
        builder.RegisterType<SystemAuditServiceBridge>().As<ISystemAuditService>().SingleInstance();
        builder.RegisterType<IntegrationService>().As<IIntegrationService>().SingleInstance();
        builder.RegisterType<ActivityService>().As<IActivityService>().SingleInstance();
        builder.RegisterType<DomainService>().As<IDomainService>().SingleInstance();
        builder.RegisterType<DashboardService>().As<IDashboardService>().SingleInstance();
    }

    protected override void LoadHubs(ContainerBuilder builder)
    {
        builder
            .RegisterType<StreamingLicenseHub>()
            .As<IStreamingLicenseHub>()
            .As<ILicenseForwarder>()
            .SingleInstance();
            
        builder
            .RegisterType<StreamingDashboardHub>()
            .As<IStreamingDashboardHub>()
            .As<IDashboardForwarder>()
            .SingleInstance();
            
        builder
            .RegisterType<StreamingSystemAuditHub>()
            .As<IStreamingSystemAuditHub>()
            .As<ISystemAuditForwarder>()
            .SingleInstance();
        
        builder
            .Register(context => new StreamingDetectionHub(context.Resolve<ILogger<StreamingDetectionHub>>(), CollectorMode.Bridge))
            .As<IStreamingDetectionHub>()
            .As<IDetectionForwarder>()
            .SingleInstance();
        
        builder
            .Register(context => new StreamingRuleHub(context.Resolve<ILogger<StreamingRuleHub>>(), CollectorMode.Bridge))
            .As<IStreamingRuleHub>()
            .As<IRuleForwarder>()
            .SingleInstance();
        
        builder
            .RegisterType<StreamingEventHub>()
            .As<IStreamingEventHub>()
            .As<IEventForwarder>()
            .SingleInstance();
    }

    protected override void LoadHostedServices(ContainerBuilder builder)
    {
        builder.RegisterType<GarbageCollectionHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<DomainHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<DatabaseHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<TracingHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<LicenseHostedService>().As<IHostedService>().SingleInstance();
        builder.Register(context => hostFactory(context.Resolve<IServiceProvider>())).As<IHostedService>().SingleInstance();
        builder.RegisterType<DashboardHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<SystemAuditHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<IntegrationHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<RuleHostedService>().As<IHostedService>().SingleInstance();
    }
}