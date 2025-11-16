using System.Collections.Concurrent;
using Collector.Databases.Abstractions.Domain.Detections;
using Collector.Databases.Abstractions.Stores.Detections;

namespace Collector.Databases.Implementation.Stores.Detections;

public sealed class DetectionStore : IDetectionStore
{
    public ConcurrentDictionary<string, DetectionCount> DetectionCountByRuleId { get; } = new();
    public ConcurrentDictionary<DetectionMitreKey, long> DetectionMitres { get; } = new();
        
    public void Delete(string ruleId)
    {
        DetectionCountByRuleId.Remove(ruleId, out _);
        foreach (var mitre in DetectionMitres)
        {
            if (mitre.Key.RuleId.Equals(ruleId))
            {
                DetectionMitres.Remove(mitre.Key, out _);
            }
        }
    }

    public void Delete()
    {
        DetectionCountByRuleId.Clear();
        DetectionMitres.Clear();
    }

    public void Add(string ruleId, long updated)
    {
        DetectionCountByRuleId.AddOrUpdate(ruleId, addValueFactory: _ => new DetectionCount(count: 1L, updated), updateValueFactory: (_, current) =>
        {
            current.Count++;
            current.Updated = Math.Max(current.Updated, updated);
            return current;
        });
    }
        
    public void Add(string ruleId, string level, string mitreId, string tactic, string technique, string subTechnique, string computer, long count = 1L)
    {
        if (string.IsNullOrWhiteSpace(tactic)) return;
        DetectionMitres.AddOrUpdate(new DetectionMitreKey(ruleId, level, mitreId, tactic, technique, subTechnique, computer), addValueFactory: _ => count, updateValueFactory: (_, current) => count == 1L ? ++current : count);
    }

    public void Add(string ruleId, long count, long updated)
    {
        DetectionCountByRuleId.AddOrUpdate(ruleId, addValueFactory: _ => new DetectionCount(count, updated), updateValueFactory: (_, current) =>
        {
            current.Count = count;
            current.Updated = Math.Max(current.Updated, updated);
            return current;
        });
    }
}