using Collector.ActiveDirectory.Managers;
using Shared.Helpers;

namespace Collector.Services.Implementation.Agent.EventLogs.Helpers;

public static class GroupPolicyObjectHelper
{
    public static readonly string LocalGpoPath = $@"{EnvironmentVariableHelper.GetSystemPath()}\SYSVOL\domain\Policies\{GroupPolicyManager.GroupPolicyObjectGuid:B}";
}