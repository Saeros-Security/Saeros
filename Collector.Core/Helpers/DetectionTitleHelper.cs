using System.Text.RegularExpressions;

namespace Collector.Core.Helpers;

public static class DetectionTitleHelper
{
    private static readonly Regex AlertOrDescriptionRegex = new("^Rule: Attack.*?Alert=(.*?)(?:,|\\s\\xa6)|^Rule: Attack.*?Desc=(.*?)(?:,|\\s\\xa6)", RegexOptions.Compiled);

    public static string GetTitle(string details, string title)
    {
        if (!string.IsNullOrWhiteSpace(details))
        {
            var match = AlertOrDescriptionRegex.Match(details);
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    value = match.Groups[2].Value;
                }
                
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return title;
    }
}