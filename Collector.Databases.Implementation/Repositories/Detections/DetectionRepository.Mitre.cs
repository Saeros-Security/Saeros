using Collector.Detection.Mitre;
using Shared.Models.Console.Responses;
using Shared.Models.Detections;

namespace Collector.Databases.Implementation.Repositories.Detections;

public sealed partial class DetectionRepository
{
    public MitreSatellite GetMitreSatellite()
    {
        var heatmapPoints = new List<HeatmapPointMetric>();
        var records = _detectionStore.DetectionMitres.GroupBy(kvp => new { kvp.Key.Level, kvp.Key.Tactic }).ToDictionary(kvp => kvp.Key, kvp => kvp.Sum(i => i.Value));
        var xaxis = Enum.GetNames<DetectionSeverity>().ToList();
        var yaxis = MitreAttackResolver.Components.Values.Select(value => value.Tactic).Distinct().OrderDescending().ToList();
        if (records.Count == 0)
        {
            foreach (var tactic in yaxis)
            {
                foreach (var level in xaxis)
                {
                    heatmapPoints.Add(new HeatmapPointMetric(xaxis.FindIndex(x => x.Equals(level, StringComparison.OrdinalIgnoreCase)), yaxis.FindIndex(y => y.Equals(tactic, StringComparison.OrdinalIgnoreCase)), weight: 0));
                }
            }
            
            return new MitreSatellite(new TacticMetric(heatmapPoints, xaxis, yaxis));
        }
        
        foreach (var record in records)
        {
            heatmapPoints.Add(new HeatmapPointMetric(xaxis.FindIndex(level => level.Equals(record.Key.Level, StringComparison.OrdinalIgnoreCase)), yaxis.FindIndex(tactic => tactic.Equals(record.Key.Tactic, StringComparison.OrdinalIgnoreCase)), weight: record.Value));
        }
        
        foreach (var tactic in yaxis)
        {
            foreach (var level in xaxis)
            {
                if (!heatmapPoints.Any(point => ((int)point.X).Equals(xaxis.FindIndex(x => x.Equals(level, StringComparison.OrdinalIgnoreCase))) &&
                                                ((int)point.Y).Equals(yaxis.FindIndex(y => y.Equals(tactic, StringComparison.OrdinalIgnoreCase)))))
                {
                    heatmapPoints.Add(new HeatmapPointMetric(xaxis.FindIndex(x => x.Equals(level, StringComparison.OrdinalIgnoreCase)), yaxis.FindIndex(y => y.Equals(tactic, StringComparison.OrdinalIgnoreCase)), weight: 0));
                }
            }
        }
        
        return new MitreSatellite(new TacticMetric(heatmapPoints, xaxis, yaxis));
    }

    public MitresByMitreId GetMitresByMitreId()
    {
        var detectionCountByMitre = new Dictionary<string, DetectionCountWithMitre>();
        var records = _detectionStore.DetectionMitres.GroupBy(kvp => kvp.Key.MitreId).ToDictionary(kvp => kvp.Key, kvp => kvp);
        
        foreach(var record in records)
        {
            var mitre = record.Value.First();
            detectionCountByMitre.Add(record.Key, new DetectionCountWithMitre(record.Value.Sum(v => v.Value), new Mitre(record.Key, mitre.Key.Tactic, mitre.Key.Technique, mitre.Key.SubTechnique)));
        }

        foreach (var kvp in MitreAttackResolver.Components)
        {
            if (!detectionCountByMitre.ContainsKey(kvp.Key))
            {
                detectionCountByMitre.Add(kvp.Key, new DetectionCountWithMitre(0, new Mitre(kvp.Value.Id, kvp.Value.Tactic, kvp.Value.Technique, kvp.Value.SubTechnique)));
            }
        }
        
        return new MitresByMitreId(detectionCountByMitre);
    }
}