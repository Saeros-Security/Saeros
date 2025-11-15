using Collector.Databases.Abstractions.Domain.Rules;
using Shared;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Streaming;
using Exclusion = Shared.Models.Console.Responses.Exclusion;

namespace Collector.Databases.Abstractions.Repositories.Detections;

public interface IDetectionRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    ValueTask InsertAsync(DetectionContract detectionContract, CancellationToken cancellationToken);
    Task<Shared.Models.Detections.Detections> GetAsync(int limit, long beforeId, long beforeDate, DetectionQuery? query, CancellationToken cancellationToken);
    Task<Shared.Models.Console.Responses.Detection?> GetAsync(long id, CancellationToken cancellationToken);
    Task<IEnumerable<Computer>> GetComputersAsync(CancellationToken cancellationToken);
    Task<IEnumerable<string>> GetTodayActiveRulesAsync(CancellationToken cancellationToken);
    Task<IEnumerable<string>> GetActiveRulesAsync(CancellationToken cancellationToken);
    Task<WinEvent?> GetWinEventAsync(long id, CancellationToken cancellationToken);
    Task<IList<RuleCountAndSeverity>> GetRuleCountAndSeverityAsync(CancellationToken cancellationToken);
    Task<IDictionary<string, long>> GetTodayRuleCountByIdsAsync(CancellationToken cancellationToken);
    Task DeleteRulesAsync(IList<DeleteRule> rules, CancellationToken cancellationToken);
    Task<Timeline> GetTimelineAsync(DetectionQuery query, int step, CancellationToken cancellationToken);
    MitreSatellite GetMitreSatellite();
    MitresByMitreId GetMitresByMitreId();
    Task Exclude(CreateRuleExclusion createRuleExclusion, CancellationToken cancellationToken);
    Task DeleteExclusion(int exclusionId, CancellationToken cancellationToken);
    Task<IEnumerable<Exclusion>> GetExclusions(Func<string, Task<Rule?>> getRule, CancellationToken cancellationToken);
    Task ComputeMetricsAsync(CancellationToken cancellationToken);
}