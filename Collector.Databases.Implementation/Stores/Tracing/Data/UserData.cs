using Collector.Databases.Implementation.Stores.Tracing.Buckets.Users;

namespace Collector.Databases.Implementation.Stores.Tracing.Data;

internal sealed class UserData(string sid, string domain, string name, string logonType, bool privileged, string sourceIp, string sourceHostname, string logon) : TracingData(nameof(User))
{
    public string Sid { get; } = sid;
    public string Domain { get; } = domain;
    public string Name { get; } = name;
    public string LogonType { get; } = logonType;
    public bool Privileged { get; } = privileged;
    public string SourceIp { get; } = sourceIp;
    public string SourceHostname { get; } = sourceHostname;
    public string Logon { get; } = logon;
}