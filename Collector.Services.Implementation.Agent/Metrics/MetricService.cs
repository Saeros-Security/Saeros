using System.Diagnostics;
using System.Reflection;
using App.Metrics;
using Collector.ActiveDirectory.Helpers;
using Collector.Core;
using Collector.Core.Helpers;
using Collector.Databases.Implementation.Helpers;
using Collector.Services.Abstractions.Metrics;
using Microsoft.Extensions.Logging;
using Shared.Helpers;
using Streaming;

namespace Collector.Services.Implementation.Agent.Metrics;

public abstract class MetricService(ILogger<MetricService> logger, IMetricsRoot metrics) : IMetricService
{
    private static readonly string OSDescription;

    static MetricService()
    {
        OSDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
    }
    
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var computer = MachineNameHelper.FullyQualifiedName;
                var osDescription = OSDescription;
                var upTime = Environment.TickCount64;
                var cpuUsage = await GetCpuUsageForProcess();
                var workingSet = Environment.WorkingSet;
                metrics.Measure.Histogram.Update(MetricOptions.CpuUsage, Convert.ToInt64(cpuUsage));
                metrics.Measure.Histogram.Update(MetricOptions.MemoryUsage, workingSet);
                var cpuContext = metrics.Snapshot.GetForContext(MetricOptions.CpuUsage.Context);
                var memoryContext = metrics.Snapshot.GetForContext(MetricOptions.MemoryUsage.Context);
                var cpuHistogram = cpuContext.Histograms.Single(h => h.Name.Contains(MetricOptions.CpuUsage.Name, StringComparison.OrdinalIgnoreCase));
                var memoryHistogram = memoryContext.Histograms.Single(h => h.Name.Contains(MetricOptions.MemoryUsage.Name, StringComparison.OrdinalIgnoreCase));
                var metricContract = new MetricContract
                {
                    Computer = computer,
                    IpAddress =  await IpAddressResolver.GetIpAddressAsync(computer, cancellationToken),
                    OperatingSystem = osDescription,
                    Uptime = upTime,
                    MedianCpuUsage = Convert.ToInt64(cpuHistogram.Value.LastValue),
                    MedianWorkingSet = Convert.ToInt64(memoryHistogram.Value.LastValue),
                    Version = Assembly.GetAssembly(typeof(MetricService)).GetCollectorVersion()
                };

                var domainJoined = DomainHelper.DomainJoined;
                if (domainJoined)
                {
                    metricContract.Domain = DomainHelper.DomainName;
                    metricContract.PrimaryDomainController = ActiveDirectoryHelper.GetPrimaryDomainControllerDnsName(logger, DomainHelper.DomainName, cancellationToken);
                    metricContract.DomainControllerCount = ActiveDirectoryHelper.EnumerateDomainControllers(logger, DomainHelper.DomainName, cancellationToken).Count();
                }
                else
                {
                    metricContract.ClearDomain();
                    metricContract.ClearPrimaryDomainController();
                    metricContract.DomainControllerCount = 0;
                }
                
                Store(metricContract);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
    }

    protected abstract void Store(MetricContract metricContract);
    
    private static async Task<double> GetCpuUsageForProcess()
    {
        var watch = Stopwatch.StartNew();
        var startCpuUsage = Environment.CpuUsage.TotalTime; 
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        watch.Stop();
        var endCpuUsage = Environment.CpuUsage.TotalTime; 
        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = Convert.ToDouble(watch.ElapsedMilliseconds); 
        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed); 
        return cpuUsageTotal * 100d;
    }
}