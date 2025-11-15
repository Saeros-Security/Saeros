namespace Collector.Databases.Abstractions.Domain.Tracing;

public sealed record TraceRecord(string Bucket, string Key, string Hash, DateTimeOffset Date, byte[] Value, long? LogonId, string? UserName, string? UserSid, string? IpAddressUser, string? WorkstationName, string? ProcessName);