using System.Text.RegularExpressions;
using Collector.Detection.Events.Lookups;
using Collector.Detection.Rules;
using Collector.Detection.Rules.Extensions;
using Shared;
using Shared.Extensions;

namespace Collector.Detection.Events.Details;

public static class DetectionDetailsResolver
{
    private static readonly Regex DetailValues = new("\\%(\\w+)\\%", RegexOptions.Compiled);
    private static readonly Regex AccessMaskMatch = new("(%%\\d{4})", RegexOptions.Compiled);
    private const string Space = " ";
    private const string AccessMask = "AccessMask";
    
    public static WinEvent Resolve(string provider, string providerGuid, string channel, string systemTime, string computer, string eventId, string? ruleDetails, string details)
    {
        var systemData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { WinEventExtensions.EventIdKey, eventId },
            { WinEventExtensions.ChannelKey, channel },
            { WinEventExtensions.ProviderNameKey, provider },
            { WinEventExtensions.ProviderGuidKey, providerGuid },
            { WinEventExtensions.SystemTimeKey, systemTime },
            { WinEventExtensions.ComputerKey, computer }
        };
        
        if (!string.IsNullOrEmpty(ruleDetails))
        {
            var eventData = GetEventData(ruleDetails, details);
            return new WinEvent(systemData, eventData);
        }

        if (EventDetails.Instance.Details.Items.TryGetValue(new ProviderEventId(provider, eventId), out var eventDetail))
        {
            var eventData = GetEventData(eventDetail, details);
            return new WinEvent(systemData, eventData);
        }

        return new WinEvent(systemData, eventData: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static IDictionary<string, string> GetEventData(string eventDetail, string details)
    {
        var eventData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var propertyMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in eventDetail.Split(Rules.Builders.Constants.DetailSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var split = part.SplitOnce(Rules.Builders.Constants.SemicolonString);
            propertyMapping.TryAdd(new string(split.Left).Trim(), DetailValues.Match(new string(split.Right.Trim())).Groups[1].Value);
        }
            
        foreach (var part in details.Split(Rules.Builders.Constants.DetailSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var split = part.SplitOnce(Rules.Builders.Constants.SemicolonString);
            if (propertyMapping.TryGetValue(new string(split.Left).Trim(), out var propertyName))
            {
                eventData.TryAdd(propertyName, new string(split.Right).Trim());   
            }
        }

        return eventData;
    }
    
    public static DetectionDetails Resolve(WinEvent winEvent, RuleMetadata ruleMetadata)
    {
        var eventTitle = string.Empty;
        var channelEventIds = winEvent.GetChannelEventIds();
        var providerEventIds = winEvent.GetProviderEventIds();
        foreach (var channelEventId in channelEventIds)
        {
            if (EventDetails.Instance.EventTitles.Items.TryGetValue(channelEventId, out var title))
            {
                eventTitle = title;
                break;
            }
        }

        var details = string.Empty;
        if (!string.IsNullOrEmpty(ruleMetadata.Details))
        {
            details = FormatDetails(winEvent, channelEventIds, ruleMetadata.Details);
        }
        else
        {
            foreach (var providerEventId in providerEventIds)
            {
                if (EventDetails.Instance.Details.Items.TryGetValue(providerEventId, out var eventDetail))
                {
                    details = FormatDetails(winEvent, channelEventIds, eventDetail);
                    break;
                }
            }
        }

        return new DetectionDetails(eventTitle, details, ruleMetadata, winEvent.GetSystemTime());
    }

    private static string FormatDetails(WinEvent winEvent, ISet<ChannelEventId> channelEventIds, string details)
    {
        foreach (Match m in DetailValues.Matches(details))
        {
            var name = m.Groups[1].Value;
            var value = winEvent.GetValue(name);
            if (string.IsNullOrEmpty(value)) continue;
            var found = false;
            foreach (var channelEventId in channelEventIds)
            {
                if (EventDetails.Instance.PropertyMappings.Items.TryGetValue(channelEventId, out var propertyMapping))
                {
                    found = true;
                    if (propertyMapping.PropertyValueByNames.TryGetValue(name, out var values))
                    {
                        if (name.Equals(AccessMask, StringComparison.Ordinal))
                        {
                            var masks = new List<string>();
                            foreach (Match match in AccessMaskMatch.Matches(value))
                            {
                                if (values.TryGetValue(match.Value, out var accessMask))
                                {
                                    masks.Add(accessMask);
                                }
                            }

                            if (masks.Count > 0)
                            {
                                details = details.Replace(m.Value, string.Join(Space, masks));
                            }
                        }
                        else
                        {
                            if (values.TryGetValue(value, out var replacedValue))
                            {
                                details = details.Replace(m.Value, replacedValue);
                            }
                            else
                            {
                                details = details.Replace(m.Value, value);
                            }
                        }
                    }
                    else
                    {
                        details = details.Replace(m.Value, value);
                    }
                }
            }

            if (!found)
            {
                details = details.Replace(m.Value, value);
            }
        }

        return DetailValues.Replace(details, Constants.Unknown).Trim();
    }

    public static ISet<string> GetProperties(RuleMetadata ruleMetadata, ISet<ProviderEventId> providerEventIds)
    {
        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(ruleMetadata.Details))
        {
            foreach (Match m in DetailValues.Matches(ruleMetadata.Details))
            {
                properties.Add(m.Groups[1].Value);
            }
        }
        else
        {
            foreach (var providerEventId in providerEventIds)
            {
                if (EventDetails.Instance.Details.Items.TryGetValue(providerEventId, out var eventDetail))
                {
                    foreach (Match m in DetailValues.Matches(eventDetail))
                    {
                        properties.Add(m.Groups[1].Value);
                    }
                }
            }
        }

        return properties;
    }
}