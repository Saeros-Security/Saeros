using Autofac;
using Collector.Core.HostedServices.GarbageCollection;
using Collector.Core.Hubs.Detections;
using Collector.Core.Hubs.Events;
using Collector.Core.Hubs.Metrics;
using Collector.Core.Hubs.Processes;
using Collector.Core.Hubs.Rules;
using Collector.Core.Hubs.SystemAudits;
using Collector.Core.Hubs.Tracing;
using Collector.Core.Modules;
using Collector.Core.Services;
using Collector.Databases.Abstractions.Repositories.AuditPolicies;
using Collector.Databases.Abstractions.Repositories.RuleConfigurations;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Contexts.AuditPolicies;
using Collector.Databases.Implementation.Contexts.RuleConfigurations;
using Collector.Databases.Implementation.Repositories.AuditPolicies;
using Collector.Databases.Implementation.Repositories.RuleConfigurations;
using Collector.Databases.Implementation.Stores.Logon.Domain;
using Collector.Databases.Implementation.Stores.Logon.Machine;
using Collector.Detection.Aggregations.Interfaces;
using Collector.Detection.Aggregations.Repositories;
using Collector.Services.Abstractions.Databases;
using Collector.Services.Abstractions.Detections;
using Collector.Services.Abstractions.DomainControllers;
using Collector.Services.Abstractions.EventLogs;
using Collector.Services.Abstractions.EventProviders;
using Collector.Services.Abstractions.Metrics;
using Collector.Services.Abstractions.Privileges;
using Collector.Services.Abstractions.Processes;
using Collector.Services.Abstractions.Rules;
using Collector.Services.Abstractions.Tracing;
using Collector.Services.Abstractions.Updates;
using Collector.Services.Implementation.Agent.Detections;
using Collector.Services.Implementation.Agent.DomainControllers;
using Collector.Services.Implementation.Agent.EventLogs;
using Collector.Services.Implementation.Agent.EventLogs.Consumers.ETW.Processes;
using Collector.Services.Implementation.Agent.EventProviders;
using Collector.Services.Implementation.Agent.Helpers;
using Collector.Services.Implementation.Agent.HostedServices.Aggregations;
using Collector.Services.Implementation.Agent.HostedServices.DomainControllers;
using Collector.Services.Implementation.Agent.HostedServices.EventLogs;
using Collector.Services.Implementation.Agent.HostedServices.EventProviders;
using Collector.Services.Implementation.Agent.HostedServices.Metrics;
using Collector.Services.Implementation.Agent.HostedServices.Privileges;
using Collector.Services.Implementation.Agent.HostedServices.Updates;
using Collector.Services.Implementation.Agent.Metrics;
using Collector.Services.Implementation.Agent.Privileges;
using Collector.Services.Implementation.Agent.Processes;
using Collector.Services.Implementation.Agent.Rules;
using Collector.Services.Implementation.Agent.SystemAudits;
using Collector.Services.Implementation.Agent.Tracing;
using Collector.Services.Implementation.Agent.Updates;
using Collector.Services.Implementation.Databases;
using Collector.Services.Implementation.Geolocation;
using Collector.Services.Implementation.HostedServices.Databases;
using Collector.Services.Implementation.HostedServices.Rules;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Databases.Collector;
using Shared.Helpers;
using Shared.Streaming.Interfaces;

namespace Collector.Services.Implementation.Agent;

public sealed class AgentModule<T> : CoreModule where T : IHostedService
{
    protected override void LoadDatabases(ContainerBuilder builder)
    {
        builder.RegisterType<RuleConfigurationContext>().SingleInstance();
        builder.RegisterType<AuditPoliciesContext>().SingleInstance();
        builder.RegisterType<RuleConfigurationsRepository>().As<IRuleConfigurationsRepository>().SingleInstance();
        builder.RegisterType<AuditPoliciesRepository>().As<IAuditPoliciesRepository>().SingleInstance();
        builder.Register(context => new AggregationRepository(context.Resolve<ILogger<AggregationRepository>>(), context.Resolve<IHostApplicationLifetime>(), CollectorContextBase.DbPath)).As<IAggregationRepository>().SingleInstance();
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
        builder.RegisterType<EventProviderService>().As<IEventProviderServiceWriter>().As<IEventProviderServiceReader>().SingleInstance();
        builder.RegisterType<RuleServiceAgent>().As<IRuleService>().SingleInstance();
        builder.RegisterType<SystemAuditServiceAgent>().As<ISystemAuditService>().SingleInstance();
        builder.RegisterType<PrivilegeServiceAgent>().As<IPrivilegeService>().SingleInstance();
        builder.RegisterType<EventLogServiceAgent>().As<IEventLogService>().SingleInstance();
        builder.RegisterType<UpdateServiceAgent>().As<IUpdateService>().SingleInstance();
        builder.RegisterType<GeolocationService>().As<IGeolocationService>().SingleInstance();
        builder.RegisterType<DetectionServiceAgent>().As<IDetectionService>().SingleInstance();
        builder.RegisterType<MetricServiceAgent>().As<IMetricService>().SingleInstance();
        builder.RegisterType<DomainControllerService>().As<IDomainControllerService>().SingleInstance();
        builder.RegisterType<ProcessTreeServiceAgent>().As<IProcessTreeService>().SingleInstance();
        builder.RegisterType<TracingAgent>().As<ITracingService>().SingleInstance();
        builder.RegisterType<PeService>().As<IPeService>().SingleInstance();
        builder.RegisterType<ProcessLifecycleObserver>().As<IProcessLifecycleObserver>().SingleInstance();
    }

    protected override void LoadHubs(ContainerBuilder builder)
    {
        builder
            .Register(context => new StreamingDetectionHub(context.Resolve<ILogger<StreamingDetectionHub>>(), CollectorMode.Agent))
            .As<IStreamingDetectionHub>()
            .As<IDetectionForwarder>()
            .SingleInstance();

        builder
            .RegisterType<StreamingProcessHub>()
            .As<IStreamingProcessHub>()
            .As<IProcessTreeForwarder>()
            .SingleInstance();
        
        builder
            .RegisterType<StreamingTraceHub>()
            .As<IStreamingTraceHub>()
            .As<ITracingForwarder>()
            .SingleInstance();
        
        builder
            .RegisterType<StreamingEventHub>()
            .As<IStreamingEventHub>()
            .As<IEventForwarder>()
            .SingleInstance();

        builder
            .RegisterType<StreamingMetricHub>()
            .As<IStreamingMetricHub>()
            .As<IMetricForwarder>()
            .SingleInstance();

        builder
            .Register(context => new StreamingRuleHub(context.Resolve<ILogger<StreamingRuleHub>>(), CollectorMode.Agent))
            .As<IStreamingRuleHub>()
            .As<IRuleForwarder>()
            .SingleInstance();

        builder
            .RegisterType<StreamingSystemAuditHub>()
            .As<IStreamingSystemAuditHub>()
            .As<ISystemAuditForwarder>()
            .SingleInstance();
    }

    protected override void LoadHostedServices(ContainerBuilder builder)
    {
        builder.RegisterType<UpdateHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<GarbageCollectionHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<MetricHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<DatabaseHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<DomainControllerHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<T>().As<IHostedService>().SingleInstance();
        builder.RegisterType<PrivilegeHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<EventProviderHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<RuleHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<AggregationHostedService>().As<IHostedService>().SingleInstance();
        builder.RegisterType<EventLogHostedService>().As<IHostedService>().SingleInstance();
    }
    
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AgentCertificateHelper>().SingleInstance();
        builder.RegisterType<RulePropertiesProvider>().As<IProvideRuleProperties>().SingleInstance();
        base.Load(builder);
    }
}