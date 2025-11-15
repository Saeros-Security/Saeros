using Shared.Models.Console.Responses;

namespace Collector.Databases.Abstractions.Repositories.Dashboards;

public interface IDashboardRepository
{
    Task StoreHomeMetricsAsync(byte[] serializedMetrics, CancellationToken cancellationToken);
    Task StoreRuleMetrics(IDictionary<string, long> ruleCountById, CancellationToken cancellationToken);
    Task<IEnumerable<HomeMetrics>> GetHomeMetricsAsync(CancellationToken cancellationToken);
    Task<IEnumerable<RuleMetrics>> GetRuleMetrics(string ruleId, CancellationToken cancellationToken);
    Task ApplyRetentionAsync(CancellationToken cancellationToken);
    Task DeleteDashboards(CancellationToken cancellationToken);
}