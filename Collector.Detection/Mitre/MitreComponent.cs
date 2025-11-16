namespace Collector.Detection.Mitre;

public class MitreComponent(string id, string tactic, string technique, string subTechnique)
{
    public string Id { get; } = id;
    public string Tactic { get; } = tactic;
    public string Technique { get; } = technique;
    public string SubTechnique { get; } = subTechnique;
}