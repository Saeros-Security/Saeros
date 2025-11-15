using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Baksteen.Extensions.DeepCopy;
using Detection.Helpers;
using Detection.Yaml;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;

namespace Collector.Detection.Converters;

internal sealed partial class LogSourceConverter
{
    private readonly ILogger _logger;

    public LogSourceConverter(ILogger logger, string sigmaRule)
    {
        _logger = logger;
        SigmaRule = sigmaRule;

        var servicesMapping = YamlParser.DeserializeMany<Dictionary<string, object>>(MappingHelper.GetMapping(MappingHelper.CategoryMapping.Services)).Single();
        var sysmonCategoryMapping = YamlParser.DeserializeMany<Dictionary<string, object>>(MappingHelper.GetMapping(MappingHelper.CategoryMapping.Sysmon)).Single();
        var builtinCategoryMapping = YamlParser.DeserializeMany<Dictionary<string, object>>(MappingHelper.GetMapping(MappingHelper.CategoryMapping.Builtin)).Single();

        var serviceMap = CreateServiceMap(servicesMapping);
        LogSourceMap = MergeCategoryMap(serviceMap, collection: [CreateCategoryMap(sysmonCategoryMapping, serviceMap), CreateCategoryMap(builtinCategoryMapping, serviceMap), CreateCategoryMap(servicesMapping, serviceMap)]);

        var processCreationFieldMap = CreateFieldMap(key: "fieldmappings_process", builtinCategoryMapping);
        var registryFieldMap = CreateFieldMap(key: "fieldmappings_registry", builtinCategoryMapping);
        var networkFieldMap = CreateFieldMap(key: "fieldmappings_network", builtinCategoryMapping);
        var antivirusFieldMap = CreateFieldMap(key: "fieldmappings_antivirus", builtinCategoryMapping);
        var wmiFieldMap = CreateFieldMap(key: "fieldmappings_wmi", builtinCategoryMapping);

        FieldMap = new Dictionary<string, Dictionary<string, string>>
        {
            { "process_creation", processCreationFieldMap },
            { "antivirus", antivirusFieldMap },
            { "registry_set", registryFieldMap },
            { "registry_add", registryFieldMap },
            { "registry_event", registryFieldMap },
            { "registry_delete", registryFieldMap },
            { "network_connection", networkFieldMap },
            { "wmi_event", wmiFieldMap }
        };
    }

    private const string EventId = "EventID";
    private const string SubjectUserName = "SubjectUserName";
    private const string LogSourceString = "logsource";
    private const string LogSourcesString = "logsources";
    private const string Service = "service";
    private const string Category = "category";
    private const string Detection = "detection";
    private const string Selection = "selection";
    private const string Condition = "condition";
    private const string Conditions = "conditions";
    private const string Rewrite = "rewrite";
    private const string Correlation = "correlation";
    private const string Type = "type";
    private const string Fields = "fields";
    private const string EventCount = "event_count";
    private const string ValueCount = "value_count";
    private const string Temporal = "temporal";
    private const string TemporalOrdered = "temporal_ordered";
    private const string RuleType = "ruletype";
    private const string Author = "author";
    private const string Tags = "tags";
    private const string Sysmon = "sysmon";
    private const string Antivirus = "antivirus";
    private const string Windows = "windows";
    private const string Security = "security";
    private const string Product = "product";
    private const string Sigma = "Sigma";
    private const string Id = "id";
    private const string Rules = "rules";
    private const string Title = "title";
    private const string Related = "related";
    private const string Derived = "derived";
    private const string Date = "date";
    private const string Channel = "Channel";
    private static readonly HashSet<string> ConvertibleFields = ["all", "base64", "base64offset", "cidr", "contains", "endswith", "endswithfield", "equalsfield", "cased", "exists", "expand", "re", "i", "m", "s", "startswith", "windash", "fieldref", "gt", "gte", "lt", "lte", "utf16", "utf16be", "utf16le", "wide"];

    private string SigmaRule { get; }
    private Dictionary<string, List<LogSource>> LogSourceMap { get; }
    private Dictionary<string, Dictionary<string, string>> FieldMap { get; }
    private List<(bool Sysmon, Dictionary<string, object> Rules)> SigmaConverted { get; } = [];
    private List<(bool Sysmon, List<Dictionary<string, object>> Rules)> SigmaCorrelationConverted { get; } = [];

    private Dictionary<string, object> TransformFields(bool sysmonRule, string category, Dictionary<string, object> map, ref bool compatible)
    {
        var result = new Dictionary<string, object>(map);
        foreach (var originalField in map.Keys)
        {
            if (originalField.Equals(EventId, StringComparison.OrdinalIgnoreCase))
            {
                if (map.TryGetValue(originalField, out var eventId))
                {
                    if (eventId is IEnumerable<string> eventIds)
                    {
                        foreach (var @event in eventIds)
                        {
                            if (sysmonRule && int.TryParse(@event, out var value) && !SysmonEventRegistry.EventMapping.ContainsKey(value))
                            {
                                compatible = false;
                            }
                        }
                    }
                    else if (eventId is string @event)
                    {
                        if (sysmonRule && int.TryParse(@event, out var value) && !SysmonEventRegistry.EventMapping.ContainsKey(value))
                        {
                            compatible = false;
                        }
                    }
                }
            }

            if (FieldMap.TryGetValue(category, out var fieldMap))
            {
                foreach (var key in fieldMap.Keys)
                {
                    if (string.Equals(originalField, key, StringComparison.OrdinalIgnoreCase))
                    {
                        var newKey = fieldMap[originalField];
                        result[newKey] = ConvertSpecialValue(newKey, map[originalField]);
                        result.Remove(originalField);
                    }
                    else if (originalField.StartsWith(key) && originalField[key.Length] == '|')
                    {
                        var newKey = fieldMap[key] + originalField[key.Length..];
                        result[newKey] = ConvertSpecialValue(fieldMap[key], map[originalField]);
                        result.Remove(originalField);
                    }
                }
            }
        }

        var shallowCopy = new Dictionary<string, object>(result);
        foreach (var (key, value) in shallowCopy)
        {
            if (string.Equals(key, SubjectUserName, StringComparison.OrdinalIgnoreCase))
            {
                if (value is not string field) continue;
                result[key] = BeforeBackSlash().Replace(field, string.Empty);
                result[SubjectUserName] = AfterBackSlash().Replace(field, string.Empty);
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    private object TransformFieldRecursively(bool sysmon, string category, object value, bool needFieldConversion, ref bool compatible)
    {
        if (!needFieldConversion)
        {
            return value;
        }

        if (value is IDictionary map)
        {
            var fields = TransformFields(sysmon, category, map is Dictionary<string, string> mapStrings ? mapStrings.ToDictionary(kvp => kvp.Key, object (kvp) => kvp.Value) : (Dictionary<string, object>)map, ref compatible);
            map.Clear();
            foreach (var kvp in fields)
            {
                map[kvp.Key] = kvp.Value;
            }

            if (map is Dictionary<string, object> mapValues)
            {
                foreach (var inner in mapValues.Select(kvp => kvp.Value))
                {
                    switch (inner)
                    {
                        case Dictionary<string, object> dictionary:
                            TransformFieldRecursively(sysmon, category, dictionary, needFieldConversion, ref compatible);
                            break;
                        case IEnumerable<object> enumerable:
                        {
                            foreach (var item in enumerable)
                            {
                                TransformFieldRecursively(sysmon, category, item, needFieldConversion, ref compatible);
                            }

                            break;
                        }
                    }
                }
            }
        }
        else if (value is IEnumerable<object> enumerable)
        {
            foreach (var item in enumerable)
            {
                TransformFieldRecursively(sysmon, category, item, needFieldConversion, ref compatible);
            }
        }

        return value;
    }

    private IEnumerable<LogSource> GetLogSources(Dictionary<string, object> map)
    {
        if (!map.TryGetValue(LogSourceString, out var logSource))
        {
            yield break;
        }

        if (logSource is Dictionary<string, object> dictionary)
        {
            if (dictionary.TryGetValue(Service, out var services))
            {
                if (services is IEnumerable<object> enumerable)
                {
                    var sources = new List<LogSource>();
                    foreach (var service in enumerable.OfType<string>())
                    {
                        if (LogSourceMap.TryGetValue(service, out var logSources))
                        {
                            sources.AddRange(logSources);
                        }
                        else
                        {
                            throw new Exception($"This rule has unsupported service: [{service}]. Conversion skipped.");
                        }
                    }

                    foreach (var source in sources)
                    {
                        yield return source;
                    }
                }
                else if (services is string service)
                {
                    if (service.Contains(','))
                    {
                        foreach (var item in service.Split(','))
                        {
                            if (LogSourceMap.TryGetValue(item, out var logSources))
                            {
                                foreach (var source in logSources)
                                {
                                    yield return source;
                                }
                            }
                            else
                            {
                                throw new Exception($"This rule has unsupported service: [{service}]. Conversion skipped.");
                            }
                        }
                    }
                    else
                    {
                        if (LogSourceMap.TryGetValue(service, out var logSources))
                        {
                            foreach (var source in logSources)
                            {
                                yield return source;
                            }
                        }
                        else
                        {
                            throw new Exception($"This rule has unsupported service: [{service}]. Conversion skipped.");
                        }
                    }
                }
            }
            else if (dictionary.TryGetValue(Category, out var value))
            {
                if (value is string category)
                {
                    if (LogSourceMap.TryGetValue(category, out var logSources))
                    {
                        foreach (var source in logSources)
                        {
                            yield return source;
                        }
                    }
                    else
                    {
                        throw new Exception($"This rule has unsupported category: [{category}]. Conversion skipped.");
                    }
                }
                else if (value is IEnumerable<object> categories)
                {
                    var sources = new List<LogSource>();
                    foreach (var source in categories.OfType<string>())
                    {
                        if (LogSourceMap.TryGetValue(source, out var logSources))
                        {
                            sources.AddRange(logSources);
                        }
                        else
                        {
                            throw new Exception($"This rule has unsupported category: [{source}]. Conversion skipped.");
                        }
                    }

                    foreach (var source in sources)
                    {
                        yield return source;
                    }
                }
            }
        }
    }

    private static bool CorrelationTypeSupported(Dictionary<string, object> correlation, [MaybeNullWhen(true)] out string error)
    {
        error = null;
        //var correlationTypes = new HashSet<string> { EventCount, ValueCount, Temporal, TemporalOrdered }; // TODO: handle temporal
        var correlationTypes = new HashSet<string> { EventCount, ValueCount };
        if (correlation.GetValueOrDefault(Type, string.Empty) is string correlationType && !correlationTypes.Contains(correlationType))
        {
            error = "This rule has unsupported correlation type";
            return false;
        }

        return true;
    }

    private bool TryGetLogSources(Dictionary<string, object> map, [MaybeNullWhen(false)] out HashSet<LogSource> logSources, [MaybeNullWhen(true)] out string error)
    {
        error = null;
        logSources = null;
        if (map.TryGetValue(Correlation, out var value) && value is Dictionary<string, object> correlation)
        {
            if (!CorrelationTypeSupported(correlation, out error)) return false;

            logSources = GetLogSources(map).ToHashSet();
            return true;
        }

        var keys = GetTerminalKeysRecursive(map[Detection]);
        var modifiers = keys.Where(key => key.Contains('|')).Select(key => BeforePipeRegex().Replace(key, string.Empty)).ToHashSet();
        if (modifiers.Any() && modifiers.Any(modifier => !ConvertibleFields.Contains(modifier)))
        {
            error = $"This rule has incompatible field: {YamlParser.Serialize(map[Detection])}";
            return false;
        }

        if (map[Detection] is string condition && (condition.Contains('%') || condition.Contains("->") || condition.Contains(" near ")))
        {
            error = $"Invalid character in condition [{condition}]";
            return false;
        }

        logSources = GetLogSources(map).ToHashSet();
        return true;
    }

    private IEnumerable<(Dictionary<string, dynamic> Rule, bool Sysmon)> ConvertRules(Dictionary<string, object> map)
    {
        map.TryAdd(Author, "Saeros");
        if (map.ContainsKey(RuleType))
        {
            map.TryAdd(Date, DateTime.Now.ToString("dd/MM/yyyy"));
            yield return (map, Sysmon: false);
        }
        else
        {
            var title = (string)map[Title];
            var previousIdentifier = map.TryGetValue(Id, out var id) ? (string)id : string.Empty;
            var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes($"{title}"));
            var rule = new Dictionary<string, dynamic>
            {
                [Title] = title,
                [Id] = new Guid(hashBytes).ToString()
            };
            
            foreach (var (key, value) in map)
            {
                if (string.Equals(key, Id, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(previousIdentifier))
                {
                    if (!map.TryGetValue(Related, out var related))
                    {
                        rule[Related] = new List<Dictionary<string, string>> { new() { { Id, previousIdentifier } }, new() { { Type, Derived } } };
                    }
                    else if (related is IEnumerable<object> enumerable)
                    {
                        var relatedCollection = enumerable.Cast<Dictionary<string, string>>().ToList();
                        if (relatedCollection.All(x => x.ContainsKey(Id) && !string.Equals(x[Id], previousIdentifier, StringComparison.OrdinalIgnoreCase)))
                        {
                            relatedCollection.Add(new Dictionary<string, string> { { Id, previousIdentifier }, { Type, Derived } });
                        }

                        rule[Related] = relatedCollection;
                    }
                }
                else if (key != Related)
                {
                    rule[key] = value;
                }
            }
            
            rule.TryAdd(RuleType, Sigma);
            rule.TryAdd(Date, DateTime.Now.ToString("dd/MM/yyyy"));
            yield return (rule, Sysmon: false);
        }
    }

    private IEnumerable<(Dictionary<string, dynamic> Rule, bool Sysmon)> ConvertRules(IEnumerable<LogSource> logSources, Dictionary<string, object> map)
    {
        map.TryAdd(Author, "Saeros");
        if (map.ContainsKey(RuleType))
        {
            map.TryAdd(Date, DateTime.Now.ToString("dd/MM/yyyy"));
            yield return (map, logSources.All(source => source.Service.Equals(Sysmon)));
        }
        else
        {
            foreach (var logSource in logSources)
            {
                var rule = CreateRule(map, logSource.GetHash());
                var sysmon = logSource.Service.Equals(Sysmon);
                if (sysmon)
                {
                    if (!rule.TryGetValue(Tags, out var tagsValue))
                    {
                        rule[Tags] = new List<string> { Sysmon };
                    }
                    else if (tagsValue is IEnumerable<object> enumerableValue)
                    {
                        var enumerableCollection = enumerableValue.OfType<string>().ToList();
                        if (!enumerableCollection.Contains(Sysmon))
                        {
                            enumerableCollection.Add(Sysmon);
                        }

                        rule[Tags] = enumerableCollection;
                    }
                }
                else if (logSource.Category == Antivirus)
                {
                    rule[LogSourceString][Product] = Windows;
                    rule[LogSourceString][Service] = logSource.Service;
                }

                if (rule.TryGetValue(Tags, out var tags) && tags is IEnumerable<object> enumerable)
                {
                    var tagsValue = enumerable.OfType<string>().ToList();
                    if (tagsValue.Any(tag => tag.Contains('_')))
                    {
                        rule[Tags] = tagsValue.Select(tag => tag.Replace("_", "-")).ToList();
                    }
                }

                if (map.TryGetValue(Detection, out var detectionValue) && detectionValue is IDictionary<string, object> detection)
                {
                    rule[Detection] = new Dictionary<string, object>();
                    rule[Detection][logSource.GetIdentifierForDetection(detection.Keys.ToList())] = logSource.GetDetection();

                    var compatible = true;
                    foreach (var (key, value) in detection)
                    {
                        rule[Detection][DotRegex().Replace(key, "_")] = TransformFieldRecursively(sysmon, logSource.Category, value, logSource.NeedFieldConversion(sysmon), ref compatible);
                    }

                    if (!compatible) continue;
                    var condition = (string)rule[Detection][Condition];
                    if ((condition.EndsWith(Selection) || condition.Contains("selection and ")) && !logSource.IsDetectable(rule[Detection], checkOnlySelection: true))
                    {
                        _logger.LogDebug($"Could not convert rule [{SigmaRule}]: This rule has incompatible field: {YamlParser.Serialize(rule[Detection])}. Conversion skipped.");
                        continue;
                    }

                    if (!condition.Contains(" of ") && !logSource.IsDetectable(rule[Detection]))
                    {
                        _logger.LogDebug($"Could not convert rule [{SigmaRule}]: This rule has incompatible field: {YamlParser.Serialize(rule[Detection])}. Conversion skipped.");
                        continue;
                    }

                    var fieldMap = FieldMap.GetValueOrDefault(logSource.Category, new Dictionary<string, string>());
                    rule[Detection][Condition] = logSource.GetCondition(sysmon, condition, detection.Keys.ToList(), fieldMap);

                    if (logSource.NeedFieldConversion(sysmon) && rule.TryGetValue(Fields, out var fields) && fields is IEnumerable<object> fieldsEnumerable)
                    {
                        var fieldsCollection = fieldsEnumerable.OfType<string>().ToList();
                        var convertedFields = fieldsCollection.Where(field => fieldMap.ContainsKey(field)).Select(field => fieldMap[field]).ToList();
                        var notConvertedFields = fieldsCollection.Where(field => !fieldMap.ContainsKey(field)).ToList();
                        rule[Fields] = convertedFields.Concat(notConvertedFields).ToList();
                    }
                }

                rule.TryAdd(RuleType, Sigma);
                yield return (rule, sysmon);
            }
        }
    }

    public bool TryConvert(bool sysmonInstalled, [MaybeNullWhen(false)] out string yml, [MaybeNullWhen(true)] out string error)
    {
        yml = null;
        error = null;
        try
        {
            var yamlNodes = YamlParser.DeserializeMany<Dictionary<string, object>>(SigmaRule).ToList();
            if (yamlNodes.Count == 1 && yamlNodes.All(node => !node.ContainsKey(Correlation)))
            {
                var yamlNode = yamlNodes.Single();
                if (!TryGetLogSources(yamlNode, out var logSources, out error)) return false;
                foreach (var (rule, sysmon) in ConvertRules(logSources, yamlNode.DeepCopy()!))
                {
                    SigmaConverted.Add((sysmon, rule));
                }

                var flattened = Flatten(SigmaConverted);
                SigmaConverted.Clear();
                foreach (var value in flattened)
                {
                    SigmaConverted.Add(value);
                }
            }
            else
            {
                var sysmonConverted = new List<Dictionary<string, object>>();
                var builtinConverted = new List<Dictionary<string, object>>();
                var sysmonUuidList = new List<string>();
                var builtinUuidList = new List<string>();
                foreach (var yamlNode in yamlNodes.OrderBy(map => map.ContainsKey(Detection) ? 0 : -1))
                {
                    if (yamlNode.ContainsKey(Correlation))
                    {
                        foreach (var (rule, sysmon) in ConvertRules(yamlNode.DeepCopy()!))
                        {
                            if (rule.TryGetValue(Correlation, out var correlationValue) && correlationValue is Dictionary<string, object> correlation)
                            {
                                if (!CorrelationTypeSupported(correlation, out error)) return false;
                            }

                            if (sysmon)
                            {
                                if (rule.TryGetValue(Id, out var value) && value is string id)
                                {
                                    sysmonUuidList.Add(id);
                                }

                                sysmonConverted.Add(rule);
                            }
                            else
                            {
                                if (rule.TryGetValue(Id, out var value) && value is string id)
                                {
                                    builtinUuidList.Add(id);
                                }

                                builtinConverted.Add(rule);
                            }
                        }
                        
                        continue;
                    }
                    
                    if (!TryGetLogSources(yamlNode, out var logSources, out error)) return false;
                    foreach (var (rule, sysmon) in ConvertRules(logSources, yamlNode.DeepCopy()!))
                    {
                        if (sysmon)
                        {
                            if (rule.TryGetValue(Id, out var value) && value is string id)
                            {
                                sysmonUuidList.Add(id);
                            }

                            sysmonConverted.Add(rule);
                        }
                        else
                        {
                            if (rule.TryGetValue(Id, out var value) && value is string id)
                            {
                                builtinUuidList.Add(id);
                            }

                            builtinConverted.Add(rule);
                        }
                    }
                }

                if (sysmonConverted.Count > 0)
                {
                    sysmonConverted[0] = CreateRule(sysmonConverted[0], logSourceHash: Sysmon);
                    if (ReferencedRuleIsUuid(sysmonConverted[0]))
                    {
                        var deepCopy = new Dictionary<string, dynamic>(sysmonConverted[0]);
                        deepCopy[Correlation][Rules] = sysmonUuidList;
                        sysmonConverted[0] = deepCopy;
                    }

                    SigmaCorrelationConverted.Add((Sysmon: true, Rules: sysmonConverted));
                }

                if (builtinConverted.Count > 0)
                {
                    builtinConverted[0] = CreateRule(builtinConverted[0], logSourceHash: Security);
                    if (ReferencedRuleIsUuid(builtinConverted[0]))
                    {
                        var deepCopy = new Dictionary<string, dynamic>(builtinConverted[0]);
                        deepCopy[Correlation][Rules] = builtinUuidList;
                        builtinConverted[0] = deepCopy;
                    }

                    SigmaCorrelationConverted.Add((Sysmon: false, Rules: builtinConverted));
                }
            }

            return TryDumpYml(sysmonInstalled, out yml, out error);
        }
        catch (YamlException exception)
        {
            error = exception.ToString();
            return false;
        }
    }

    private static List<(bool Sysmon, Dictionary<string, object> Rules)> Flatten(List<(bool Sysmon, Dictionary<string, object> Rules)> rules)
    {
        return Flatten(rules.Select(item => (item.Sysmon, new List<Dictionary<string, object>> { item.Rules })).ToList());
    }

    private static List<(bool Sysmon, Dictionary<string, object> Rules)> Flatten(List<(bool Sysmon, List<Dictionary<string, object>> Rules)> rules)
    {
        var flattened = new List<(bool Sysmon, Dictionary<string, object> Rules)>();
        foreach (var group in rules.GroupBy(rule => rule.Sysmon))
        {
            var key = Guid.NewGuid().ToString();
            var values = group.SelectMany(kvp => kvp.Rules).Aggregate((left, right) =>
            {
                if (left[Detection] is Dictionary<string, object> detectionLeft && right[Detection] is Dictionary<string, object> detectionRight)
                {
                    var (categoryLeft, _) = detectionLeft.First();
                    var (categoryRight, _) = detectionRight.First();
                    detectionLeft[key] = detectionLeft.TryGetValue(key, out var value) && value is string aggregation ? $"{aggregation}__{categoryRight}" : $"{categoryLeft}__{categoryRight}";
                }
                
                return left;
            });

            if (values[Detection] is Dictionary<string, object> map && map.TryGetValue(key, out var aggregatedCategories) && aggregatedCategories is string aggregatedCategoriesString)
            {
                var (categoryLeft, logSourceLeft) = map.First();
                map[aggregatedCategoriesString] = logSourceLeft;
                
                var condition = (string)map[Condition];
                var categories= aggregatedCategoriesString.Split("__", StringSplitOptions.RemoveEmptyEntries);
                map[Condition] = condition.Replace(categoryLeft, $"({string.Join(" or ", categories)})");
                foreach (var category in categories)
                {
                    foreach (var dictionary in group.SelectMany(value => value.Rules))
                    {
                        if (dictionary[Detection] is Dictionary<string, object> detection && detection.TryGetValue(category, out var value))
                        {
                            map[category] = value;
                        }
                    }
                }

                map.Remove(aggregatedCategoriesString);
                map.Remove(key);
            }

            flattened.Add((group.Key, values));
        }

        return flattened;
    }
    
    private static string DumpYaml(List<Dictionary<string, object>> rules)
    {
        var sb = new StringBuilder();
        foreach (var rule in rules)
        {
            sb.Append(YamlParser.Serialize(rule));
            sb.AppendLine("---");
        }

        return sb.ToString();
    }

    private bool TryDumpYml(bool sysmonInstalled, [MaybeNullWhen(false)] out string yml, [MaybeNullWhen(true)] out string error)
    {
        yml = null;
        error = null;
        var yamlRules = new List<string>();
        if (sysmonInstalled)
        {
            yamlRules.Add(SigmaConverted.Any(rule => rule.Sysmon) ?
                DumpYaml(SigmaConverted.Where(rule => rule.Sysmon).Select(rule => rule.Rules).ToList()) :
                DumpYaml(SigmaConverted.Where(rule => !rule.Sysmon).Select(rule => rule.Rules).ToList()));
            
            yamlRules.Add(SigmaCorrelationConverted.Any(rule => rule.Sysmon) ?
                DumpYaml(SigmaCorrelationConverted.Where(rule => rule.Sysmon).SelectMany(rule => rule.Rules).ToList()) :
                DumpYaml(SigmaCorrelationConverted.Where(rule => !rule.Sysmon).SelectMany(rule => rule.Rules).ToList()));
        }
        else
        {
            yamlRules.Add(DumpYaml(SigmaConverted.Where(rule => !rule.Sysmon).Select(rule => rule.Rules).ToList()));
            yamlRules.Add(DumpYaml(SigmaCorrelationConverted.Where(rule => !rule.Sysmon).SelectMany(rule => rule.Rules).ToList()));
        }
        
        yml = string.Join(Environment.NewLine, yamlRules.Where(yamlRule => !string.IsNullOrWhiteSpace(yamlRule)));
        if (!sysmonInstalled && (SigmaConverted.Count > 0 && SigmaConverted.All(rule => rule.Sysmon) || SigmaCorrelationConverted.Count > 0 && SigmaCorrelationConverted.All(rule => rule.Sysmon)))
        {
            error = "The rule requires Sysmon to be installed and this configuration is not supported";
            return false;
        }
        
        return !string.IsNullOrWhiteSpace(yml);
    }

    private static Dictionary<string, dynamic> CreateRule(Dictionary<string, object> map, string logSourceHash)
    {
        var title = (string)map[Title];
        var previousIdentifier = map.TryGetValue(Id, out var id) ? (string)id : string.Empty;
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes($"{title}{logSourceHash}"));
        var rule = new Dictionary<string, dynamic>
        {
            [Title] = title,
            [Id] = new Guid(hashBytes).ToString()
        };

        foreach (var (key, value) in map)
        {
            if (string.Equals(key, Id, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(previousIdentifier))
            {
                if (!map.TryGetValue(Related, out var related))
                {
                    rule[Related] = new List<Dictionary<string, string>> { new() { { Id, previousIdentifier } }, new() { { Type, Derived } } };
                }
                else if (related is IEnumerable<object> enumerable)
                {
                    var relatedCollection = enumerable.Cast<Dictionary<string, string>>().ToList();
                    if (relatedCollection.All(x => x.ContainsKey(Id) && !string.Equals(x[Id], previousIdentifier, StringComparison.OrdinalIgnoreCase)))
                    {
                        relatedCollection.Add(new Dictionary<string, string> { { Id, previousIdentifier }, { Type, Derived } });
                    }

                    rule[Related] = relatedCollection;
                }
            }
            else if (key != Related)
            {
                rule[key] = value;
            }
        }

        rule.TryAdd(Date, DateTime.Now.ToString("dd/MM/yyyy"));
        return rule;
    }

    private static bool ReferencedRuleIsUuid(Dictionary<string, object> map)
    {
        if (!map.TryGetValue(Correlation, out var correlation) || correlation is not Dictionary<string, object> dictionary || !dictionary.TryGetValue(Rules, out var rules) || rules is not IEnumerable<object> enumerable)
            return false;

        return enumerable.Cast<string>().All(ruleId => Guid.TryParse(ruleId, out _));
    }

    private static List<string> GetTerminalKeysRecursive(object map, List<string>? keys = null)
    {
        if (keys == null)
            keys = [];

        if (map is Dictionary<string, object> dictionary)
        {
            foreach (var (key, value) in dictionary)
            {
                keys.Add(key);
                if (value is Dictionary<string, object>)
                {
                    GetTerminalKeysRecursive(value, keys);
                }
                else if (value is IEnumerable<object> enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is Dictionary<string, object>)
                        {
                            GetTerminalKeysRecursive(item, keys);
                        }
                    }
                }
            }
        }

        return keys;
    }

    private static object ConvertSpecialValue(string key, object value)
    {
        return key switch
        {
            "ProcessId" or "NewProcessId" when value is string id => int.Parse(id).ToString("X"),
            "MandatoryLabel" when value is string mandatoryLabel => LogSource.INTEGRITY_LEVEL_VALUES.GetValueOrDefault(mandatoryLabel.ToUpper(), mandatoryLabel),
            "OperationType" when value is string operationType => LogSource.OPERATION_TYPE_VALUES.GetValueOrDefault(operationType, operationType),
            "ObjectName" when value is string objectName => objectName.Replace("HKLM", @"\REGISTRY\MACHINE").Replace("HKU", @"\REGISTRY\USER"),
            "ObjectName" when value is IEnumerable<object> objectNames => objectNames.Cast<string>().Select(v => v.Replace("HKLM", @"\REGISTRY\MACHINE").Replace("HKU", @"\REGISTRY\USER")).ToList(),
            "Direction" when value is string direction => LogSource.CONNECTION_INITIATED_VALUES.GetValueOrDefault(direction, direction),
            "Application" when value is string application => application.Replace("C:", "\\device\\harddiskvolume?"),
            "Application" when value is IEnumerable<object> applications => applications.Cast<string>().Select(v => v.Replace("C:", "\\device\\harddiskvolume?")).ToList(),
            "Protocol" when value is string protocol => LogSource.CONNECTION_PROTOCOL_VALUES.GetValueOrDefault(protocol, protocol),
            _ => value
        };
    }

    private static Dictionary<string, List<string>> CreateServiceMap(Dictionary<string, object> map)
    {
        if (!map.TryGetValue(LogSourcesString, out var value) || value is not Dictionary<string, object> logSources)
        {
            throw new Exception($"Invalid YAML. Key [{LogSourcesString}] not found.");
        }

        var serviceToChannel = new Dictionary<string, List<string>>();
        foreach (var source in logSources)
        {
            var serviceInfo = (Dictionary<string, object>)source.Value;
            if (serviceInfo.TryGetValue(Service, out var service) &&
                service is string key &&
                serviceInfo.TryGetValue(Conditions, out var dictionary) &&
                dictionary is Dictionary<string, object> conditions)
            {
                serviceToChannel[key] = conditions.TryGetValue(Channel, out var channel) ? channel is IEnumerable<object> enumerable ? enumerable.Cast<string>().ToList() : [channel.ToString()] : [];
            }
        }

        return serviceToChannel;
    }

    private static HashSet<LogSource> CreateCategoryMap(Dictionary<string, object> map, Dictionary<string, List<string>> serviceToChannel)
    {
        if (!map.TryGetValue(LogSourcesString, out var value) || value is not Dictionary<string, object> logSources)
        {
            throw new Exception($"Invalid YAML. Key [{LogSourcesString}] not found.");
        }

        var mapper = new HashSet<LogSource>();
        foreach (var source in logSources)
        {
            var sourceInfo = (Dictionary<string, dynamic>)source.Value;
            if (sourceInfo.TryGetValue(Category, out var category) &&
                category is string categoryValue &&
                sourceInfo[Rewrite] is Dictionary<string, object> rewrite)
            {
                var service = (string)rewrite[Service];
                var channels = serviceToChannel[service];
                var eventId = sourceInfo[Conditions][EventId];
                var logSource = new LogSource(categoryValue, service, channels, eventId is string s ? [int.Parse(s)] : ((IEnumerable<string>)eventId).Select(int.Parse).ToList());
                mapper.Add(logSource);
            }
        }

        return mapper;
    }
    
    private static Dictionary<string, List<LogSource>> MergeCategoryMap(Dictionary<string, List<string>> serviceMap, List<HashSet<LogSource>> collection)
    {
        var mergedMap = new Dictionary<string, List<LogSource>>();
        foreach (var logSources in collection)
        {
            foreach (var logSource in logSources)
            {
                if (!mergedMap.ContainsKey(logSource.Category))
                {
                    mergedMap[logSource.Category] = [];
                }
                
                mergedMap[logSource.Category].Add(logSource);
            }
        }

        foreach (var (serviceCategory, channels) in serviceMap)
        {
            if (!mergedMap.ContainsKey(serviceCategory))
            {
                mergedMap[serviceCategory] = [new LogSource(serviceCategory, service: string.Empty, channels, eventIds: null)];
            }
        }

        return mergedMap;
    }

    private static Dictionary<string, string> CreateFieldMap(string key, Dictionary<string, object> map)
    {
        if (!map.TryGetValue(key, out var value))
        {
            throw new Exception($"Invalid YAML. Key [{key}] not found.");
        }

        var fieldMap = (Dictionary<string, object>)value;
        var result = new Dictionary<string, string>();

        foreach (var (originalField, fieldValue) in fieldMap)
        {
            var newField = (string)fieldValue;
            result[originalField] = newField;
        }

        return result;
    }

    [GeneratedRegex(@".*\\")]
    private static partial Regex BeforeBackSlash();
    
    [GeneratedRegex(@"\\.*")]
    private static partial Regex AfterBackSlash();
    
    [GeneratedRegex(@".*\|")]
    private static partial Regex BeforePipeRegex();
    
    [GeneratedRegex(@"\.")]
    private static partial Regex DotRegex();
}