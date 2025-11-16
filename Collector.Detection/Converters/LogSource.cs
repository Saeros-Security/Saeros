using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Collector.Detection.Converters;

internal sealed partial class LogSource(string category, string service, List<string> channels, List<int>? eventIds) : IEquatable<LogSource>
{
    public string Category { get; } = category;
    public string Service { get; } = service;
    private List<string> Channels { get; } = channels;
    private List<int>? EventIds { get; } = eventIds;

    public override int GetHashCode()
    {
        return HashCode.Combine(Category, Service, Channels);
    }

    public bool Equals(LogSource? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Category == other.Category && Service == other.Service && Channels.SequenceEqual(other.Channels);
    }
    
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is LogSource other && Equals(other);
    }

    public string GetHash()
    {
        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"{Category}{Service}{string.Join(",", Channels)}"))).ToString();
    }

    public string GetIdentifierForDetection(List<string> keys)
    {
        var newIdentifier = string.IsNullOrEmpty(Category) ? Service.Replace("-", "_") : Category.Replace("-", "_");

        if (keys.Any(key => string.Equals(key, newIdentifier, StringComparison.OrdinalIgnoreCase)))
        {
            newIdentifier = "logsource_mapping_" + newIdentifier;
        }

        return newIdentifier;
    }

    public Dictionary<string, object> GetDetection()
    {
        return EventIds != null 
            ? new Dictionary<string, object> { { "EventID", EventIds.Count > 1 ? EventIds : EventIds.Single() }, { "Channel", Channels.Count > 1 ? Channels : Channels.Single() } }
            : new Dictionary<string, object> { { "Channel", Channels.Count > 1 ? Channels : Channels.Single() } };
    }

    public string GetCondition(bool sysmon, string condition, List<string> keys, Dictionary<string, string> fieldMap)
    {
        if (AggregationRegex().IsMatch(condition))
        {
            var conditionBeforePipe = $"({GetIdentifierForDetection(keys)} and {ConditionBeforePipeRegex().Match(condition).Groups[1].Value})";
            var conditionAfterPipe = ConditionAfterPipeRegex().Match(condition).Groups[1].Value;

            if (NeedFieldConversion(sysmon))
            {
                conditionAfterPipe = fieldMap.Keys.Aggregate(conditionAfterPipe, (current, field) => current.Replace(field, fieldMap[field]));
            }

            return $"{conditionBeforePipe} | {conditionAfterPipe}";
        }

        return !condition.Contains(' ') ? $"{GetIdentifierForDetection(keys)} and {condition}" : $"{GetIdentifierForDetection(keys)} and ({condition})";
    }

    public bool NeedFieldConversion(bool sysmon)
    {
        if (sysmon) return true;
        switch (Category)
        {
            case "antivirus":
            case "process_creation" when EventIds is not null && EventIds.Contains(4688):
            case "registry_set" or "registry_add" or "registry_event" or "registry_delete" when EventIds is not null && EventIds.Contains(4657):
            case "network_connection" when EventIds is not null && (EventIds.Contains(5156) || EventIds.Contains(3)):
            case "wmi_event" when EventIds is not null && EventIds.Contains(5861):
                return true;
            default:
                return false;
        }
    }

    private bool IsDetectableFields(List<string> keys, Func<List<string>, Func<string, bool>, bool> predicate)
    {
        List<string> commonFields = ["CommandLine", "ProcessId"];
        if (Category == "network_connection" && EventIds is not null && (EventIds.Contains(5156) || EventIds.Contains(3)))
        {
            commonFields = ["ProcessId", "SourcePort", "Protocol"];
        }

        keys = keys.Select(key => PipeRegex().Replace(key, string.Empty)).Where(key => !commonFields.Contains(key)).ToList();
        if (!keys.Any())
            return true;
        else if (EventIds is not null && EventIds.Contains(4688))
            return !predicate(keys, key => WINDOWS_SYSMON_PROCESS_CREATION_FIELDS.Contains(key));
        else if (EventIds is not null && EventIds.Contains(1))
            return !predicate(keys, key => WINDOWS_SECURITY_PROCESS_CREATION_FIELDS.Contains(key));
        else if (EventIds is not null && EventIds.Contains(4657))
            return !predicate(keys, key => WINDOWS_SYSMON_REGISTRY_EVENT_FIELDS.Contains(key));
        else if (EventIds is not null && (EventIds.Contains(12) || EventIds.Contains(13) || EventIds.Contains(14)))
            return !predicate(keys, key => WINDOWS_SECURITY_REGISTRY_EVENT_FIELDS.Contains(key));
        else if (EventIds is not null && EventIds.Contains(5156))
            return !predicate(keys, key => WINDOWS_SYSMON_NETWORK_EVENT_FIELDS.Contains(key));
        else if (EventIds is not null && EventIds.Contains(3))
            return !predicate(keys, key => WINDOWS_SECURITY_NETWORK_EVENT_FIELDS.Contains(key));
        return true;
    }

    public bool IsDetectable(Dictionary<string, object> map, bool checkOnlySelection = false)
    {
        if (!string.Equals(Category, "process_creation", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Category, "registry_set", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Category, "registry_add", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Category, "registry_event", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Category, "registry_delete", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Category, "network_connection", StringComparison.OrdinalIgnoreCase))
            return true;

        var nonDetectableFields = new HashSet<string> { "condition", "process_creation", "timeframe", "registry_set", "registry_add", "registry_event", "registry_delete", "network_connection" };
        foreach (var key in map.Keys)
        {
            if (checkOnlySelection && !string.Equals(key, "selection", StringComparison.OrdinalIgnoreCase))
                continue;

            if (nonDetectableFields.Contains(key))
                continue;

            var value = map[key];
            var isDetectable = true;
            switch (value)
            {
                case Dictionary<string, object> dictionary:
                    isDetectable = IsDetectableFields(dictionary.Keys.ToList(), (keys, predicate) => keys.Any(predicate));
                    break;
                case IEnumerable<object> enumerable:
                {
                    var maps = enumerable.OfType<Dictionary<string, object>>().ToList();
                    if (!maps.Any())
                        continue;
                
                    isDetectable = IsDetectableFields(maps.SelectMany(m => m.Keys).ToList(), (keys, predicate) => keys.All(predicate));
                    break;
                }
            }

            if (!isDetectable)
                return false;
        }

        return true;
    }

    private static readonly List<string> WINDOWS_SYSMON_PROCESS_CREATION_FIELDS =
    [
        "RuleName", "UtcTime", "ProcessGuid", "ProcessId", "Image", "FileVersion", "Description", "Product",
        "Company", "OriginalFileName", "CommandLine", "CurrentDirectory", "User", "LogonGuid", "LogonId",
        "TerminalSessionId", "IntegrityLevel", "Hashes", "ParentProcessGuid", "ParentProcessId", "ParentImage",
        "ParentCommandLine", "ParentUser"
    ];

    private static readonly List<string> WINDOWS_SECURITY_PROCESS_CREATION_FIELDS =
    [
        "SubjectUserSid", "SubjectUserName", "SubjectDomainName", "SubjectLogonId", "NewProcessId",
        "NewProcessName", "TokenElevationType", "ProcessId", "CommandLine", "TargetUserSid", "TargetUserName",
        "TargetDomainName", "TargetLogonId", "ParentProcessName", "MandatoryLabel"
    ];

    private static readonly List<string> WINDOWS_SYSMON_REGISTRY_EVENT_FIELDS = ["EventType", "UtcTime", "ProcessId", "ProcessGuid", "Image", "TargetObject", "Details", "NewName"];

    private static readonly List<string> WINDOWS_SECURITY_REGISTRY_EVENT_FIELDS =
    [
        "SubjectUserSid", "SubjectUserName", "SubjectDomainName", "SubjectLogonId", "ObjectName",
        "ObjectValueName", "HandleId", "OperationType", "OldValueType", "OldValue", "NewValueType", "NewValue",
        "ProcessId", "ProcessName"
    ];

    private static readonly List<string> WINDOWS_SYSMON_NETWORK_EVENT_FIELDS =
    [
        "RuleName", "UtcTime", "ProcessGuid", "ProcessId", "Image", "User", "Protocol", "Initiated",
        "SourceIsIpv6", "SourceIp", "SourceHostname", "SourcePort", "SourcePortName", "DestinationIsIpv6",
        "DestinationIp", "DestinationHostname", "DestinationPort", "DestinationPortName", "CommandLine",
        "ParentImage"
    ];

    private static readonly List<string> WINDOWS_SECURITY_NETWORK_EVENT_FIELDS =
    [
        "ProcessID", "Application", "Protocol", "Direction", "SourceAddress", "SourcePort", "DestAddress",
        "DestPort", "FilterRTID", "LayerName", "LayerRTID", "RemoteUserID", "RemoteMachineID"
    ];

    public static readonly Dictionary<string, string> INTEGRITY_LEVEL_VALUES = new()
    {
        { "LOW", "S-1-16-4096" },
        { "MEDIUM", "S-1-16-8192" },
        { "HIGH", "S-1-16-12288" },
        { "SYSTEM", "S-1-16-16384" }
    };

    public static readonly Dictionary<string, string> OPERATION_TYPE_VALUES = new()
    {
        { "CreateKey", "%%1904" },
        { "SetValue", "%%1905" },
        { "DeleteValue", "%%1906" },
        { "RenameKey", "%%1905" }
    };

    public static readonly Dictionary<string, string> CONNECTION_INITIATED_VALUES = new()
    {
        { "true", "%%14593" },
        { "false", "%%14592" }
    };

    public static readonly Dictionary<string, string> CONNECTION_PROTOCOL_VALUES = new()
    {
        { "tcp", "6" },
        { "udp", "17" }
    };

    [GeneratedRegex(@".*\s+\| (.*)")]
    private static partial Regex ConditionAfterPipeRegex();
    
    [GeneratedRegex(@"([^|].*?)(\s?\| count\(.*)")]
    private static partial Regex AggregationRegex();
    
    [GeneratedRegex(@"^(.*)\s+\|")]
    private static partial Regex ConditionBeforePipeRegex();
    
    [GeneratedRegex(@"\|.*")]
    private static partial Regex PipeRegex();
}