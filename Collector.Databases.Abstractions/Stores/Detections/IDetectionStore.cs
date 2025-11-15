using System.Collections.Concurrent;
using Collector.Databases.Abstractions.Domain.Detections;

namespace Collector.Databases.Abstractions.Stores.Detections;

public interface IDetectionStore
{
    void Add(string ruleId, long updated);
    void Add(string ruleId, string level, string mitreId, string tactic, string technique, string subTechnique, string computer, long count = 1);
    void Add(string ruleId, long count, long updated);
    ConcurrentDictionary<string, DetectionCount> DetectionCountByRuleId { get; }
    ConcurrentDictionary<DetectionMitreKey, long> DetectionMitres { get; }
    void Delete(string ruleId);
    void Delete();
}