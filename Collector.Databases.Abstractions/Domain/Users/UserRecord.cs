namespace Collector.Databases.Abstractions.Domain.Users;

public sealed class UserRecord(int id, string username, string passwordHash)
{
    public int Id { get; } = id;
    public string Username { get; } = username;
    public string PasswordHash { get; } = passwordHash;
}