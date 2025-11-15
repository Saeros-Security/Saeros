using Collector.Services.Abstractions.Privileges;

namespace Collector.Services.Implementation.Agent.Privileges;

public abstract class PrivilegeService : IPrivilegeService
{
    public abstract void SetPrivileges();
}