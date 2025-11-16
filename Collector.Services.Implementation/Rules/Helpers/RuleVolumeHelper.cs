using Collector.ActiveDirectory.AuditPolicies;
using Collector.Detection.Rules;
using Shared;

namespace Collector.Services.Implementation.Rules.Helpers;

internal static class RuleVolumeHelper
{
    private static readonly IDictionary<int, AuditPolicyVolume> VolumeByEventId = AuditPolicyMapping.EventIdBySubcategoryGuid.SelectMany(kvp => kvp.Value.Select(auditPolicyEventId => new KeyValuePair<int, Guid>(auditPolicyEventId.EventId, kvp.Key))).GroupBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Select(inner => AuditPolicyMapping.VolumeBySubcategoryGuid.TryGetValue(inner.Value, out var volume) ? volume : AuditPolicyVolume.Low).Aggregate(GetVolume));

    private static readonly IDictionary<int, AuditPolicyVolume> VolumeByEventIdOverride = new Dictionary<int, AuditPolicyVolume>
    {
        { 4776, AuditPolicyVolume.VeryHigh },
        { 5145, AuditPolicyVolume.VeryHigh },
        { 5156, AuditPolicyVolume.VeryHigh },
        { 5157, AuditPolicyVolume.VeryHigh },
        { 5379, AuditPolicyVolume.High }
    };
    
    private static AuditPolicyVolume GetVolume(AuditPolicyVolume left, AuditPolicyVolume right)
    {
        if (left == AuditPolicyVolume.VeryHigh || right == AuditPolicyVolume.VeryHigh) return AuditPolicyVolume.VeryHigh;
        if (left == AuditPolicyVolume.High || right == AuditPolicyVolume.High) return AuditPolicyVolume.High;
        if (left == AuditPolicyVolume.Medium || right == AuditPolicyVolume.Medium) return AuditPolicyVolume.Medium;
        return AuditPolicyVolume.Low;
    }

    // https://rootdse.org/posts/understanding-sysmon-events
    private static AuditPolicyVolume GetSysmonVolume(int eventId)
    {
        switch (eventId)
        {
            case 1:
                return AuditPolicyVolume.High; // Process creation
            case 2:
                return AuditPolicyVolume.Medium; // A process changed a file creation time
            case 3:
                return AuditPolicyVolume.VeryHigh; // Network creation
            case 5:
                return AuditPolicyVolume.High; // Process terminated
            case 6:
                return AuditPolicyVolume.Medium; // Driver loaded
            case 7:
                return AuditPolicyVolume.Medium; // Image loaded
            case 8:
                return AuditPolicyVolume.Medium; // Create remote thread
            case 9:
                return AuditPolicyVolume.Medium; // Raw Access read
            case 10:
                return AuditPolicyVolume.High; // ProcessAccess
            case 11:
                return AuditPolicyVolume.Medium; // File created
            case 12:
                return AuditPolicyVolume.Medium; // Registry object created/deleted
            case 13:
                return AuditPolicyVolume.Medium; // Registry value set
            case 14:
                return AuditPolicyVolume.Low; // Registry value rename
            case 15:
                return AuditPolicyVolume.Medium; // FileCreateStreamHash
            case 17:
                return AuditPolicyVolume.Medium; // Pipe created
            case 18:
                return AuditPolicyVolume.Medium; // Pipe connected
            case 19:
                return AuditPolicyVolume.Low; // WmiEventFilter registered
            case 20:
                return AuditPolicyVolume.Low; // WmiEventConsumer activity
            case 21:
                return AuditPolicyVolume.Low; // WmiEventConsumerToFilter activity
            case 22:
                return AuditPolicyVolume.VeryHigh; // DNS query
            case 23:
                return AuditPolicyVolume.Medium; // File archived
            case 24:
                return AuditPolicyVolume.Low; // Clipboard change
            case 25:
                return AuditPolicyVolume.Medium; // Process tampering
            case 26:
                return AuditPolicyVolume.Medium; // File deleted
            case 27:
                return AuditPolicyVolume.Medium; // FileBlockExecutable
            case 28:
                return AuditPolicyVolume.Medium; // FileBlockShredding
            case 29:
                return AuditPolicyVolume.Medium; // FileExecutableDetected
        }

        return AuditPolicyVolume.Low;
    }
    
    private static void ComputeVolume(RuleMetadata metadata, ref AuditPolicyVolume volume, int eventId)
    {
        var useSysmon = metadata.Tags.Contains("sysmon", StringComparer.OrdinalIgnoreCase);
        if (useSysmon)
        {
            var sysmonVolume = GetSysmonVolume(eventId);
            if (sysmonVolume > volume)
            {
                volume = GetVolume(volume, sysmonVolume);
            }
        }
        else if (VolumeByEventIdOverride.TryGetValue(eventId, out var eventIdVolume) || VolumeByEventId.TryGetValue(eventId, out eventIdVolume))
        {
            if (eventIdVolume > volume)
            {
                volume = GetVolume(volume, eventIdVolume);
            }
        }
    }
    
    public static AuditPolicyVolume ToVolume(RuleMetadata metadata, ISet<int> eventIds)
    {
        var volume = AuditPolicyVolume.Low;
        foreach (var eventId in eventIds)
        {
            ComputeVolume(metadata, ref volume, eventId);
        }

        return volume;
    }
}