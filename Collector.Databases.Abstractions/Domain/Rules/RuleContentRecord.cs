namespace Collector.Databases.Abstractions.Domain.Rules;

public sealed record RuleContentRecord(byte[] Content, bool Enabled, string GroupName);