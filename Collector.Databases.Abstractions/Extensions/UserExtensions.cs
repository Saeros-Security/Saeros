using Collector.Databases.Abstractions.Domain.Users;
using Shared.Helpers;
using Shared.Models.Console.Responses;

namespace Collector.Databases.Abstractions.Extensions;

public static class UserExtensions
{
    public static User FromUserRecord(this UserRecord probe)
    {
        return new User(probe.Username, string.Empty);
    }

    public static UserRecord FromUser(this User user)
    {
        return new UserRecord(0, user.Username, Sha1Helper.Hash(user.Password));
    }
}