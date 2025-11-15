using System.Diagnostics.CodeAnalysis; 
using Collector.Databases.Abstractions.Domain.Rules;

namespace Collector.Databases.Abstractions.Stores.Rules;

public interface IRuleStore
{
    void Add(RuleRecord record);
    void Delete();
    void Delete(string ruleId);
    bool TryGetDescription(string ruleId, [MaybeNullWhen(false)] out string description);
    bool TryGetMitre(string ruleId, out string mitreId, out string mitreTactic, out string mitreTechnique, out string mitreSubTechnique);
}