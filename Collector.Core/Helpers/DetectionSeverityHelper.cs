using System.Text.RegularExpressions;
using Shared.Models.Detections;

namespace Collector.Core.Helpers;

public static class DetectionSeverityHelper
{
    private static readonly Regex LevelRegex = new("^Rule: Attack.*?Level=(\\d?)(?:,|\\s\\xa6)", RegexOptions.Compiled);

    public static DetectionSeverity GetSeverity(string details, DetectionSeverity severity)
    {
        var match = LevelRegex.Match(details);
        if (match.Success)
        {
            var value = match.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (int.TryParse(value, out var level))
                {
                    switch (level)
                    {
                        case 0:
                            return DetectionSeverity.Informational;
                        case 1:
                            return DetectionSeverity.Low;
                        case 2:
                            return DetectionSeverity.Medium;
                        case 3:
                            return DetectionSeverity.High;
                        case 4:
                            return DetectionSeverity.Critical;
                    }
                }
            }
        }

        return severity;
    }
}