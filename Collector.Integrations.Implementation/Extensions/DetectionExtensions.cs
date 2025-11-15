using Collector.Integrations.Abstractions;
using Shared;
using Shared.Extensions;
using Shared.Helpers;
using Streaming;

namespace Collector.Integrations.Implementation.Extensions;

internal static class DetectionExtensions
{
    public static Export ToExport(this DetectionContract detection, WinEvent winEvent, string tactic, string technique, string subTechnique)
    {
        return ToExport(detection, winEvent.System, winEvent.EventData, tactic, technique, subTechnique);
    }
    
    private static Export ToExport(this DetectionContract detection, IDictionary<string, string> system, IDictionary<string, string> data, string tactic, string technique, string subTechnique)
    {
        return new Export(detection.RuleId, detection.Title, detection.Computer, new DateTimeOffset(detection.Date, TimeSpan.Zero), detection.Level.FromLevel(), Deconstruct(detection), new EventExport(detection.EventTitle, system, data), new MitreExport(tactic, technique, subTechnique));
    }
    
    private static SortedDictionary<string, string> Deconstruct(DetectionContract detection)
    {
        return DetectionHelper.Deconstruct(detection.Details);
    }
}