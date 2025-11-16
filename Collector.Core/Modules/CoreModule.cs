using App.Metrics;
using Autofac;
using Collector.Core.HostedServices;
using Collector.Core.HostedServices.Domains;
using Microsoft.Extensions.Hosting;

namespace Collector.Core.Modules;

public abstract class CoreModule : Module
{
    protected abstract void LoadDatabases(ContainerBuilder builder);
    protected abstract void LoadServices(ContainerBuilder builder);
    protected abstract void LoadHubs(ContainerBuilder builder);
    protected abstract void LoadHostedServices(ContainerBuilder builder);
    
    protected override void Load(ContainerBuilder builder)
    {
        var metricsBuilder = new MetricsBuilder();
        metricsBuilder
            .SampleWith.ForwardDecaying()
            .TimeWith.StopwatchClock()
            .Configuration.Configure(
                options =>
                {
                    options.Enabled = true;
                    options.ReportingEnabled = false;
                });

        var metrics = metricsBuilder.Build();
        builder.RegisterInstance(metrics).As<IMetricsRoot>().SingleInstance();
        
        LoadDatabases(builder);
        LoadServices(builder);
        LoadHubs(builder);
        builder.RegisterType<DomainHostedService>().As<IHostedService>().SingleInstance();
        LoadHostedServices(builder);
    }
}