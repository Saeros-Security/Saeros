using System.Text.RegularExpressions;
using Collector.Detection.Contracts;
using Collector.Detection.Rules.Mappings;
using Detection.Helpers;
using Detection.Yaml;
using nietras.SeparatedValues;
using Shared;
using Constants = Collector.Detection.Rules.Builders.Constants;

namespace Collector.Detection.Rules.Helpers;

public static partial class ConfigHelper
{
    private static readonly IDictionary<string, List<string>> Config;

    static ConfigHelper()
    {
        Config = ReadConfig();
    }
    
    private static IDictionary<string, List<string>> ReadConfig()
    {
        var nextIsStart = false;
        var nextIsContent = false;
        var name = string.Empty;
        var content = new List<string>();
        var contentByName = new Dictionary<string, List<string>>();
        foreach (var line in RuleHelper.GetConfig().Split("\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Equals("---FILE_START---", StringComparison.Ordinal))
            {
                nextIsStart = true;
                continue;
            }
            
            if (line.Equals("---CONTENT---", StringComparison.Ordinal))
            {
                nextIsContent = true;
                continue;
            }

            if (line.Equals("---FILE_END---", StringComparison.Ordinal))
            {
                nextIsContent = false;
                contentByName.Add(name, content.ToList());
                content.Clear();
                continue;
            }

            if (nextIsStart && line.Contains("path:", StringComparison.Ordinal))
            {
                name = line.Split(':', StringSplitOptions.RemoveEmptyEntries)[1].Trim();
                nextIsStart = false;
                continue;
            }
            
            if (nextIsContent)
            {
                content.Add(line.Replace("\r", string.Empty));
            }
        }

        return contentByName;
    }
    
    public static Aliases GetAliases()
    {
        const string key = "eventkey_alias.txt";
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        foreach (var line in Config.Count == 0 ? File.ReadLines(path) : Config[key])
        {
            var kvp = line.Split(Constants.Comma, StringSplitOptions.RemoveEmptyEntries);
            dictionary.TryAdd(kvp[0], kvp[1]);
        }

        return new Aliases(dictionary);
    }

    public static EventTitles GetEventTitles()
    {
        const string key = "channel_eid_info.txt";
        var dictionary = new Dictionary<ChannelEventId, string>();
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        var sep = new Sep(',');
        using var reader = Config.Count == 0 ? sep.Reader().FromFile(path) : sep.Reader().FromText(string.Join(Environment.NewLine, Config[key]));
        foreach (var readRow in reader)
        {
            var channel = readRow["Channel"].ToString();
            var eventId = readRow["EventID"].ToString();
            var eventTitle = readRow["EventTitle"].ToString();
            dictionary.TryAdd(new ChannelEventId(channel, eventId), eventTitle);
        }

        return new EventTitles(dictionary);
    }

    public static PropertyMappings GetPropertyMappings()
    {
        const string key = "data_mapping";
        var dictionary = new Dictionary<ChannelEventId, PropertyMapping>();
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}{Path.DirectorySeparatorChar}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        foreach (var pair in Config.Where(kvp => kvp.Key.Contains(key, StringComparison.OrdinalIgnoreCase)))
        {
            var mapping = YamlParser.Deserialize<YamlDataMapping>(new StringReader(string.Join(Environment.NewLine, pair.Value)));
            dictionary.TryAdd(new ChannelEventId(mapping.Channel, mapping.EventID), new PropertyMapping(Transform(mapping.RewriteFieldData.ToDictionary(kvp => kvp.Key, kvp => (List<object>)kvp.Value)), mapping.HexToDecimal));
        }

        if (Config.Count > 0)
        {
            return new PropertyMappings(dictionary);
        }

        foreach (var filePath in Directory.GetFiles(path, "*.yaml", SearchOption.AllDirectories))
        {
            var mapping = YamlParser.Deserialize<YamlDataMapping>(File.OpenRead(filePath));
            dictionary.TryAdd(new ChannelEventId(mapping.Channel, mapping.EventID), new PropertyMapping(Transform(mapping.RewriteFieldData.ToDictionary(kvp => kvp.Key, kvp => (List<object>)kvp.Value)), mapping.HexToDecimal));
        }
        
        return new PropertyMappings(dictionary);

        IDictionary<string, Dictionary<string, string>> Transform(IDictionary<string, List<object>> items)
        {
            return items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.OfType<Dictionary<string, string>>().SelectMany(dict => dict).ToDictionary());
        }
    }
    
    public static Details GetDetails()
    {
        const string key = "default_details.txt";
        var dictionary = new Dictionary<ProviderEventId, string>();
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        var sep = new Sep(',');
        using var reader = Config.Count == 0 ? sep.Reader().FromFile(path) : sep.Reader().FromText(string.Join(Environment.NewLine, Config[key]));
        foreach (var readRow in reader)
        {
            var provider = readRow["Provider"].ToString();
            var eventId = readRow[" EID"].ToString();
            var details = readRow[" Details"].ToString();
            dictionary.TryAdd(new ProviderEventId(provider, eventId), details);
        }

        // Override 4673 to include PrivilegeList
        dictionary[new ProviderEventId("Microsoft-Windows-Security-Auditing", "4673")] = "Proc: %ProcessName% ¦ User: %SubjectUserName% ¦ LID: %SubjectLogonId% ¦ Priv: %PrivilegeList%";
        return new Details(dictionary);
    }
    
    public static ChannelAbbrevations GetChannelAbbreviations()
    {
        const string key = "channel_abbreviations.txt";
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        var sep = new Sep(',');
        using var reader = Config.Count == 0 ? sep.Reader().FromFile(path) : sep.Reader().FromText(string.Join(Environment.NewLine, Config[key]));
        foreach (var readRow in reader)
        {
            var channel = readRow["Channel"].ToString();
            var abbreviation = readRow["Abbreviation"].ToString();
            dictionary.TryAdd(abbreviation, channel);
        }

        return new ChannelAbbrevations(dictionary);
    }
    
    public static ProviderAbbrevations GetProviderAbbreviations()
    {
        const string key = "provider_abbreviations.txt";
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        var sep = new Sep(',');
        using var reader = Config.Count == 0 ? sep.Reader().FromFile(path) : sep.Reader().FromText(string.Join(Environment.NewLine, Config[key]));
        foreach (var readRow in reader)
        {
            var provider = readRow["Provider"].ToString();
            var abbreviation = readRow["Abbreviation"].ToString();
            dictionary.TryAdd(abbreviation, provider);
        }

        return new ProviderAbbrevations(dictionary);
    }

    public static ExcludedRules GetExcludedRules()
    {
        const string key = "exclude_rules.txt";
        var excludedRules = new HashSet<string>(StringComparer.Ordinal);
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        foreach (var line in Config.Count > 0 ? Config[key] : File.ReadLines(path))
        {
            var match = GetGuidRegex().Match(line);
            if (match.Success)
                excludedRules.Add(match.Value);
        }

        return new ExcludedRules(excludedRules);
    }
    
    public static NoisyRules GetNoisyRules()
    {
        const string key = "noisy_rules.txt";
        var noisyRules = new HashSet<string>(StringComparer.Ordinal);
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        foreach (var line in Config.Count > 0 ? Config[key] : File.ReadLines(path))
        {
            var match = GetGuidRegex().Match(line);
            if (match.Success)
                noisyRules.Add(match.Value);
        }

        return new NoisyRules(noisyRules);
    }
    
    public static ProvenRules GetProvenRules()
    {
        const string key = "proven_rules.txt";
        var provenRules = new HashSet<string>(StringComparer.Ordinal);
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        foreach (var line in Config.Count > 0 ? Config[key] : File.ReadLines(path))
        {
            var match = GetGuidRegex().Match(line);
            if (match.Success)
                provenRules.Add(match.Value);
        }

        return new ProvenRules(provenRules);
    }
    
    public static TargetEventIds GetTargetEventIds()
    {
        const string key = "target_event_IDs.txt";
        var targetEventIds = new HashSet<int>();
        var path = Path.Join(AppContext.BaseDirectory, $"rules{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}{key}");
        if (Config.Count == 0 && !Path.Exists(path))
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        foreach (var line in Config.Count > 0 ? Config[key] : File.ReadLines(path))
        {
            targetEventIds.Add(int.Parse(line));
        }

        return new TargetEventIds(targetEventIds);
    }

    [GeneratedRegex(@"[{(]?[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}[)}]?", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex GetGuidRegex();
}