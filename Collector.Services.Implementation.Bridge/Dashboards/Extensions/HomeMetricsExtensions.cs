using Collector.Databases.Implementation.Repositories.Dashboards;
using Shared.Models.Console.Responses;

namespace Collector.Services.Implementation.Bridge.Dashboards.Extensions;

public static class HomeMetricsExtensions
{
    private static double CalculatePercentageTrend(double previous, double current)
    {
        if (previous == 0d) return 0d;
        return (current - previous) / previous;
    }

    public static HomeMetrics Consolidate(this IList<HomeMetrics> metrics, int maxDaysHistory, int activeRuleCount)
    {
        if (metrics.Count == 0)
        {
            return DashboardRepository.EmptyHomeMetrics();
        }

        var lastMetric = metrics.Last();
        var dailyActiveRulesValues = metrics.Select(m => m.ActiveRules.History.ComputeShift(m.Date, maxDaysHistory, m.ActiveRules.CurrentValue)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var dailyActiveRulesTrend = dailyActiveRulesValues.Length > 1 ? CalculatePercentageTrend(dailyActiveRulesValues[^2], dailyActiveRulesValues[^1]) : 0d;
        var dailyActiveRulesPercentage = dailyActiveRulesTrend == 0d || double.IsNaN(dailyActiveRulesTrend) ? 0d : dailyActiveRulesTrend;
        var dailyDetectionsValues = metrics.Select(m => m.Detections.History.ComputeShift(m.Date, maxDaysHistory, m.Detections.CurrentValue)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var dailyDetectionsTrend = dailyDetectionsValues.Length > 1 ? CalculatePercentageTrend(dailyDetectionsValues[^2], dailyDetectionsValues[^1]) : 0d;
        var dailyDetectionsPercentage = dailyDetectionsTrend == 0d || double.IsNaN(dailyDetectionsTrend) ? 0d : dailyDetectionsTrend;
        var computersValues = metrics.Select(m => m.Computers.History.ComputeShift(m.Date, maxDaysHistory, m.Computers.CurrentValue)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var computersTrend = computersValues.Length > 1 ? CalculatePercentageTrend(computersValues[^2], computersValues[^1]) : 0d;
        var computersPercentage = computersTrend == 0d || double.IsNaN(computersTrend) ? 0d : computersTrend;
        var criticalDetectionsValues = metrics.Select(m => new { m.DetectionsSatellite.CriticalSeverityDetections, m.Date }).GroupBy(m => m.Date).Select(m => m.Select(v => v.CriticalSeverityDetections).ToArray().ComputeShift(m.Key, maxDaysHistory, m.Single().CriticalSeverityDetections)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var highDetectionsValues = metrics.Select(m => new { m.DetectionsSatellite.HighSeverityDetections, m.Date }).GroupBy(m => m.Date).Select(m => m.Select(v => v.HighSeverityDetections).ToArray().ComputeShift(m.Key, maxDaysHistory, m.Single().HighSeverityDetections)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var mediumDetectionsValues = metrics.Select(m => new { m.DetectionsSatellite.MediumSeverityDetections, m.Date }).GroupBy(m => m.Date).Select(m => m.Select(v => v.MediumSeverityDetections).ToArray().ComputeShift(m.Key, maxDaysHistory, m.Single().MediumSeverityDetections)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var lowDetectionsValues = metrics.Select(m => new { m.DetectionsSatellite.LowSeverityDetections, m.Date }).GroupBy(m => m.Date).Select(m => m.Select(v => v.LowSeverityDetections).ToArray().ComputeShift(m.Key, maxDaysHistory, m.Single().LowSeverityDetections)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var informationDetectionsValues = metrics.Select(m => new { m.DetectionsSatellite.InformationalSeverityDetections, m.Date }).GroupBy(m => m.Date).Select(m => m.Select(v => v.InformationalSeverityDetections).ToArray().ComputeShift(m.Key, maxDaysHistory, m.Single().InformationalSeverityDetections)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var detectionsHistory = metrics.Select(m => m.DetectionsSatellite.DetectionsHistory.ComputeShift(m.Date, maxDaysHistory, m.DetectionsSatellite.Detections)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());
        var activeRulesHistory = metrics.Select(m => m.DetectionsSatellite.ActiveRulesHistory.ComputeShift(m.Date, maxDaysHistory, m.DetectionsSatellite.ActiveRules)).Aggregate((i1, i2) => i1.Zip(i2, (l, r) => l + r).ToArray());

        return new HomeMetrics(
            activeRules: new BannerMetric(lastMetric.ActiveRules.CurrentValue, dailyActiveRulesValues, dailyActiveRulesPercentage, lastMetric.Date),
            detections: new BannerMetric(lastMetric.Detections.CurrentValue, dailyDetectionsValues, dailyDetectionsPercentage, lastMetric.Date),
            computers: new BannerMetric(lastMetric.Computers.CurrentValue, computersValues, computersPercentage, lastMetric.Date),
            detectionsSatellite: new DetectionsSatellite(metrics.Sum(m => m.DetectionsSatellite.Detections), activeRuleCount, metrics.Sum(m => m.DetectionsSatellite.CriticalSeverityDetections), metrics.Sum(m => m.DetectionsSatellite.HighSeverityDetections), metrics.Sum(m => m.DetectionsSatellite.MediumSeverityDetections), metrics.Sum(m => m.DetectionsSatellite.LowSeverityDetections), metrics.Sum(m => m.DetectionsSatellite.InformationalSeverityDetections), detectionsHistory, activeRulesHistory, lastMetric.DetectionsSatellite.TotalRules, lastMetric.Date),
            new SeveritySatellite(metrics.Sum(m => m.DetectionsSatellite.CriticalSeverityDetections), metrics.Sum(m => m.DetectionsSatellite.HighSeverityDetections), metrics.Sum(m => m.DetectionsSatellite.MediumSeverityDetections), metrics.Sum(m => m.DetectionsSatellite.LowSeverityDetections), metrics.Sum(m => m.DetectionsSatellite.InformationalSeverityDetections), [informationDetectionsValues, lowDetectionsValues, mediumDetectionsValues, highDetectionsValues, criticalDetectionsValues], lastMetric.Date),
            lastMetric.MitreSatellite,
            lastMetric.RulesSatellite,
            lastMetric.EventsSatellite,
            lastMetric.TrafficSatellite,
            lastMetric.Date);
    }
}