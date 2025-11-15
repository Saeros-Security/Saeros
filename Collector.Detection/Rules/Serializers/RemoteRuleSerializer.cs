using Collector.Detection.Extensions;
using K4os.Compression.LZ4.Streams;

namespace Collector.Detection.Rules.Serializers;

public static class RemoteRuleSerializer
{
    public static MemoryStream Serialize(this StandardRule rule)
    {
        var stream = new MemoryStream();
        using var lz4Stream = LZ4Stream.Encode(stream, leaveOpen: true);
        rule.ToRemoteStream(lz4Stream);
        return stream;
    }

    public static RuleBase Deserialize(this Stream stream, RuleType ruleType)
    {
        using var source = LZ4Stream.Decode(stream, leaveOpen: true);
        return source.FromRemoteStream(ruleType);
    }
}