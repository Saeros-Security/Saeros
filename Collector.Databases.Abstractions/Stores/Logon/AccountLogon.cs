namespace Collector.Databases.Abstractions.Stores.Logon;

public sealed class AccountLogon(string targetAccount, ISet<string> targetComputer, ISet<string> logonType, ISet<string> sourceComputer, ISet<string> sourceIpAddress, long count) : IEquatable<AccountLogon>
{
    public AccountLogon(string targetAccount, string targetComputer, string logonType, string sourceComputer, string sourceIpAddress)
        : this(targetAccount, new HashSet<string> {targetComputer}, new HashSet<string> {logonType}, new HashSet<string> {sourceComputer}, new HashSet<string> {sourceIpAddress}, count: 1)
    {

    }

    public string TargetAccount { get; } = targetAccount;
    public ISet<string> TargetComputer { get; } = targetComputer;
    public ISet<string> LogonType { get; } = logonType;
    public ISet<string> SourceComputer { get; } = sourceComputer;
    public ISet<string> SourceIpAddress { get; } = sourceIpAddress;
    public long Count { get; } = count;
    
    public bool Equals(AccountLogon? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return TargetAccount == other.TargetAccount;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is AccountLogon other && Equals(other);
    }

    public override int GetHashCode()
    {
        return TargetAccount.GetHashCode();
    }
}