using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using Collector.Databases.Abstractions.Domain.Rules;
using Shared.Models.Console.Requests;
using Shared.Models.Console.Responses;
using Shared.Models.Detections;
using Streaming;

namespace Collector.Databases.Abstractions.Repositories.Rules;

public interface IRuleRepository : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    ValueTask InsertAsync(RuleContract ruleContract, CancellationToken cancellationToken);
    ValueTask UpdateAsync(string ruleId, DateTimeOffset date, CancellationToken cancellationToken);
    Task<(byte[] Content, bool Enabled)> CopyRuleAsync(string ruleId, string ruleTitle, string groupName, CancellationToken cancellationToken);
    Task EnableAsync(IList<EnableRule> rules, CancellationToken cancellationToken);
    Task DisableAsync(IList<DisableRule> rules, CancellationToken cancellationToken);
    Task DeleteAsync(IList<DeleteRule> rules, CancellationToken cancellationToken);
    Task<Shared.Models.Rules.Rules> GetAsync(CancellationToken cancellationToken);
    Task<Rule?> GetAsync(string ruleId, CancellationToken cancellationToken);
    Task<IEnumerable<RuleTitle>> GetRuleTitlesAsync(CancellationToken cancellationToken);
    IEnumerable<Mitre> GetMitres();
    Task<Rule?> GetByTitleAsync(string title, CancellationToken cancellationToken);
    Task<int> CountAsync(CancellationToken cancellationToken);
    Task<RuleAttributes?> GetAttributesAsync(string ruleId, CancellationToken cancellationToken);
    Task<string> UpdateRuleCodeAsync(string ruleId, string code, CancellationToken cancellationToken);
    IAsyncEnumerable<RuleContentRecord> EnumerateNonBuiltinRulesAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<RuleRecord> EnumerateEnabledRuleIdsAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<RuleRecord> EnumerateDisabledRuleIdsAsync(CancellationToken cancellationToken);
    Task<string?> GetRuleContentAsync(string ruleId, CancellationToken cancellationToken);
    IObservable<Unit> RuleInsertionObservable { get; }
    bool TryGetDescription(string ruleId, [MaybeNullWhen(false)] out string description);
    Task EnableRulesAsync(CancellationToken cancellationToken);
    Task<IDictionary<DetectionSeverity, SortedSet<string>>> GetRuleTitlesBySeverityAsync(CancellationToken cancellationToken);
}