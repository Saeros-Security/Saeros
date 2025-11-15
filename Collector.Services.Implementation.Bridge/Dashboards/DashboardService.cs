using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using App.Metrics;
using Collector.Core;
using Collector.Core.Extensions;
using Collector.Core.Hubs.Dashboards;
using Collector.Databases.Abstractions.Domain.Rules;
using Collector.Databases.Abstractions.Repositories.Dashboards;
using Collector.Databases.Abstractions.Repositories.Detections;
using Collector.Databases.Abstractions.Repositories.Rules;
using Collector.Databases.Abstractions.Stores.Tracing;
using Collector.Services.Abstractions.Activity;
using Collector.Services.Abstractions.Dashboards;
using Collector.Services.Implementation.Bridge.Dashboards.Extensions;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Shared.Models.Console.Responses;
using Shared.Models.Detections;
using Shared.Serialization;
using Streaming;

namespace Collector.Services.Implementation.Bridge.Dashboards;

public sealed class DashboardService(ILogger<DashboardService> logger, IActivityService activityService, IMetricsRoot metrics, IDashboardRepository dashboardRepository, IDetectionRepository detectionRepository, IRuleRepository ruleRepository, ITracingStore tracingStore, IStreamingDashboardHub dashboardHub) : IDashboardService
{
    private static readonly TimeSpan HomeDashboardInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RuleDashboardInterval = TimeSpan.FromSeconds(30);
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    
    private DateTimeOffset _lastRuleDashboardProcessingTime = DateTimeOffset.MinValue;
    
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await detectionRepository.ComputeMetricsAsync(cancellationToken);
        await Task.WhenAll(StoreRuleDashboardMetricsPeriodicallyAsync(cancellationToken), StoreHomeDashboardMetricsPeriodicallyAsync(cancellationToken));
    }
    
    private async Task StoreRuleDashboardMetricsPeriodicallyAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RuleDashboardInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (activityService.LastActive + RuleDashboardInterval * 2 >= DateTimeOffset.UtcNow || _lastRuleDashboardProcessingTime + TimeSpan.FromHours(1) < DateTimeOffset.UtcNow)
                {
                    await dashboardRepository.StoreRuleMetrics(await detectionRepository.GetTodayRuleCountByIdsAsync(stoppingToken), stoppingToken);
                    _lastRuleDashboardProcessingTime = DateTimeOffset.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Cancellation has occurred");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error has occurred");
            }
        }
    }
    
    private DateTimeOffset _lastHomeDashboardProcessingTime = DateTimeOffset.MinValue;
    private async Task StoreHomeDashboardMetricsPeriodicallyAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(HomeDashboardInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (activityService.LastActive + HomeDashboardInterval * 2 >= DateTimeOffset.UtcNow || _lastHomeDashboardProcessingTime + TimeSpan.FromHours(1) < DateTimeOffset.UtcNow)
                {
                    var ruleCountTask = ruleRepository.CountAsync(stoppingToken);
                    var todayActiveRulesTask = detectionRepository.GetTodayActiveRulesAsync(stoppingToken);
                    var activeRulesTask = detectionRepository.GetActiveRulesAsync(stoppingToken);
                    var ruleCountAndSeverityTask = detectionRepository.GetRuleCountAndSeverityAsync(stoppingToken);
                    var ruleTitlesBySeverityTask = ruleRepository.GetRuleTitlesBySeverityAsync(stoppingToken);

                    await Task.WhenAll(ruleCountTask, todayActiveRulesTask, activeRulesTask, ruleCountAndSeverityTask, ruleTitlesBySeverityTask);
                    if (TryGetHomeMetrics(await ruleCountTask, (await todayActiveRulesTask).Count(), GetMitreSatellite(), await ruleCountAndSeverityTask, await ruleTitlesBySeverityTask, out var currentDashboard))
                    {
                        var serializedMetrics = JsonSerializer.Serialize(currentDashboard, SerializationContext.Default.HomeMetrics).LZ4CompressString();
                        await dashboardRepository.StoreHomeMetricsAsync(serializedMetrics, stoppingToken);
                    }

                    var dashboards = await dashboardRepository.GetHomeMetricsAsync(stoppingToken);
                    var consolidatedDashboard = dashboards.OrderBy(d => d.Date).TakeLast(Core.Constants.MaxRetentionDays).ToList().Consolidate(Core.Constants.MaxRetentionDays, (await activeRulesTask).Count());
                    dashboardHub.SendDashboard(new DashboardContract
                    {
                        HomeData = ByteString.CopyFromUtf8(JsonSerializer.Serialize(consolidatedDashboard, SerializationContext.Default.HomeMetrics))
                    });

                    try
                    {
                        if (_stopwatch.Elapsed >= TimeSpan.FromDays(1))
                        {
                            await dashboardRepository.ApplyRetentionAsync(stoppingToken);
                            _stopwatch.Restart();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogWarning("Cancellation has occurred");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error has occurred");
                    }
                    
                    _lastHomeDashboardProcessingTime = DateTimeOffset.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Cancellation has occurred");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error has occurred");
            }
        }
    }

    private bool TryGetHomeMetrics(int ruleCount, int activeRuleCount, MitreSatellite mitreSatellite, IList<RuleCountAndSeverity> ruleCountAndSeverities, IDictionary<DetectionSeverity, SortedSet<string>> ruleTitlesBySeverity, [MaybeNullWhen(false)] out HomeMetrics homeMetrics)
    {
        homeMetrics = null;

        var today = DateTime.Today.Date;
        var detectionContext = metrics.Snapshot.GetForContext(MetricOptions.Detections.Context);
        var computerContext = metrics.Snapshot.GetForContext(MetricOptions.Computers.Context);
        var eventThroughputContext = metrics.Snapshot.GetForContext(MetricOptions.EventThroughput.Context);

        var detections = detectionContext.Counters.Where(h => h.Name.Contains(MetricOptions.Detections.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var computers = computerContext.Gauges.Where(h => h.Name.Contains(MetricOptions.Computers.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var histograms = eventThroughputContext.Histograms.Where(h => h.Name.Contains(MetricOptions.EventThroughput.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var histogram = histograms.SingleOrDefault();

        var currentThroughput = histogram?.Value.LastValue ?? 0d;
        var p50Throughput = histogram?.Value.Median ?? 0d;
        var p75Throughput = histogram?.Value.Percentile75 ?? 0d;
        var p95Throughput = histogram?.Value.Percentile95 ?? 0d;

        var detectionCount = detections.GetFromToday().Sum(r => r.Value.Count);
        var computerCount = computers.GetFromToday().Sum(r => r.Value);
        
        var activeRuleBanner = new BannerMetric(activeRuleCount, history: Array.Empty<double>(), trend: 0d, today);
        var detectionBanner = new BannerMetric(detectionCount, history: Array.Empty<double>(), trend: 0d, today);
        var computerBanner = new BannerMetric(computerCount, history: Array.Empty<double>(), trend: 0d, today);

        var criticalDetections = detections.Where(r => r.Tags.Values.Contains(Enum.GetName(DetectionSeverity.Critical), StringComparer.OrdinalIgnoreCase)).ToArray().GetFromToday().Sum(r => r.Value.Count);
        var highDetections = detections.Where(r => r.Tags.Values.Contains(Enum.GetName(DetectionSeverity.High), StringComparer.OrdinalIgnoreCase)).ToArray().GetFromToday().Sum(r => r.Value.Count);
        var mediumDetections = detections.Where(r => r.Tags.Values.Contains(Enum.GetName(DetectionSeverity.Medium), StringComparer.OrdinalIgnoreCase)).ToArray().GetFromToday().Sum(r => r.Value.Count);
        var lowDetections = detections.Where(r => r.Tags.Values.Contains(Enum.GetName(DetectionSeverity.Low), StringComparer.OrdinalIgnoreCase)).ToArray().GetFromToday().Sum(r => r.Value.Count);
        var informationalDetections = detections.Where(r => r.Tags.Values.Contains(Enum.GetName(DetectionSeverity.Informational), StringComparer.OrdinalIgnoreCase)).ToArray().GetFromToday().Sum(r => r.Value.Count);

        var severitySatellite = new SeveritySatellite(
            criticalImpactDetections: (int)criticalDetections,
            highImpactDetections: (int)highDetections,
            mediumImpactDetections: (int)mediumDetections,
            lowImpactDetections: (int)lowDetections,
            informationalImpactDetections: (int)informationalDetections,
            history: new[] { Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>() },
            today);

        var detectionsSatellite = new DetectionsSatellite(
            detections: (int)detectionCount,
            activeRules: activeRuleCount,
            criticalSeverityDetections: (int)criticalDetections,
            highSeverityDetections: (int)highDetections,
            mediumSeverityDetections: (int)mediumDetections,
            lowSeverityDetections: (int)lowDetections,
            informationalSeverityDetections: (int)informationalDetections,
            detectionsHistory: Array.Empty<int>(),
            activeRulesHistory: Array.Empty<int>(),
            totalRules: ruleCount,
            today);

        var rulesSatellite = new RulesSatellite(critical: ruleCountAndSeverities.Where(r => r.Severity == DetectionSeverity.Critical).Take(5).OrderByDescending(r => r.Count).ToDictionary(kvp => kvp.RuleTitle, kvp => kvp.Count),
            high: ruleCountAndSeverities.Where(r => r.Severity == DetectionSeverity.High).OrderByDescending(r => r.Count).Take(5).ToDictionary(kvp => kvp.RuleTitle, kvp => kvp.Count),
            medium: ruleCountAndSeverities.Where(r => r.Severity == DetectionSeverity.Medium).OrderByDescending(r => r.Count).Take(5).ToDictionary(kvp => kvp.RuleTitle, kvp => kvp.Count),
            low: ruleCountAndSeverities.Where(r => r.Severity == DetectionSeverity.Low).OrderByDescending(r => r.Count).Take(5).ToDictionary(kvp => kvp.RuleTitle, kvp => kvp.Count),
            informational: ruleCountAndSeverities.Where(r => r.Severity == DetectionSeverity.Informational).OrderByDescending(r => r.Count).Take(5).ToDictionary(kvp => kvp.RuleTitle, kvp => kvp.Count));
        var eventsSatellite = new EventsSatellite(tracingStore.GetEventCountById(), currentThroughput, p50Throughput, p75Throughput, p95Throughput);
        var outbound = tracingStore.GetOutboundValues();
        var trafficSatellite = new TrafficSatellite(outbound.OutboundByCountry, outbound.Entries);
        
        FillRulesSatellite(rulesSatellite, r => r.Critical, DetectionSeverity.Critical, ruleTitlesBySeverity);
        FillRulesSatellite(rulesSatellite, r => r.High, DetectionSeverity.High, ruleTitlesBySeverity);
        FillRulesSatellite(rulesSatellite, r => r.Medium, DetectionSeverity.Medium, ruleTitlesBySeverity);
        FillRulesSatellite(rulesSatellite, r => r.Low, DetectionSeverity.Low, ruleTitlesBySeverity);
        FillRulesSatellite(rulesSatellite, r => r.Informational, DetectionSeverity.Informational, ruleTitlesBySeverity);
        
        homeMetrics = new HomeMetrics(activeRuleBanner, detectionBanner, computerBanner, detectionsSatellite, severitySatellite, mitreSatellite, rulesSatellite, eventsSatellite, trafficSatellite, today);
        return true;
    }

    private MitreSatellite GetMitreSatellite() => detectionRepository.GetMitreSatellite();

    private static void FillRulesSatellite(RulesSatellite rankingSatellite, Func<RulesSatellite, IDictionary<string, long>> selector, DetectionSeverity severity, IDictionary<DetectionSeverity, SortedSet<string>> ruleTitlesBySeverity)
    {
        var item = selector(rankingSatellite);
        if (ruleTitlesBySeverity.TryGetValue(severity, out var ruleTitles))
        {
            foreach (var title in ruleTitles)
            {
                if (item.Count >= 5) break;
                if (item.ContainsKey(title)) continue;
                item.TryAdd(title, 0);
            }
        }
    }
}