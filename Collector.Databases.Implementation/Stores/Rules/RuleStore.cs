using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Collector.Databases.Abstractions.Domain.Rules;
using Collector.Databases.Abstractions.Stores.Rules;

namespace Collector.Databases.Implementation.Stores.Rules;

public sealed class RuleStore : IRuleStore
{
    private sealed record Mitre(string MitreId, string? MitreTactic, string? MitreTechnique, string? MitreSubTechnique);

    private readonly ConcurrentDictionary<string, Mitre> _mitreByRuleId = new();
    private readonly ConcurrentDictionary<string, string> _ruleDescriptionByRuleId = new();

    public void Add(RuleRecord record)
    {
        _ruleDescriptionByRuleId[record.RuleId] = record.Description;
        if (!string.IsNullOrEmpty(record.MitreId))
        {
            _mitreByRuleId.AddOrUpdate(record.RuleId, addValueFactory: _ => new Mitre(record.MitreId, record.MitreTactic, record.MitreTechnique, record.MitreSubTechnique), updateValueFactory: (_, _) => new Mitre(record.MitreId, record.MitreTactic, record.MitreTechnique, record.MitreSubTechnique));
        }
    }

    public void Delete()
    {
        _mitreByRuleId.Clear();
        _ruleDescriptionByRuleId.Clear();
    }

    public void Delete(string ruleId)
    {
        _mitreByRuleId.Remove(ruleId, out _);
        _ruleDescriptionByRuleId.Remove(ruleId, out _);
    }

    public bool TryGetDescription(string ruleId, [MaybeNullWhen(false)] out string description)
    {
        return _ruleDescriptionByRuleId.TryGetValue(ruleId, out description);
    }

    public bool TryGetMitre(string ruleId, out string mitreId, out string mitreTactic, out string mitreTechnique, out string mitreSubTechnique)
    {
        mitreId = string.Empty;
        mitreTactic = string.Empty;
        mitreTechnique = string.Empty;
        mitreSubTechnique = string.Empty;
        if (_mitreByRuleId.TryGetValue(ruleId, out var mitre))
        {
            mitreId = mitre.MitreId;
            mitreTactic = mitre.MitreTactic ?? string.Empty;
            mitreTechnique = mitre.MitreTechnique ?? string.Empty;
            mitreSubTechnique = mitre.MitreSubTechnique ?? string.Empty;
            return true;
        }

        return false;
    }
}