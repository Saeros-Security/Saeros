using Collector.ActiveDirectory.Managers;
using Shared.Helpers;

namespace Collector.ActiveDirectory.Helpers.Sysvol;

public static class SysvolHelper
{
    public static string GetCompanyDirectory()
    {
        return Path.Join(EnvironmentVariableHelper.GetSystemPath(), $"SYSVOL\\sysvol\\{DomainHelper.DomainName}\\Policies\\{GroupPolicyManager.GroupPolicyObjectGuid:B}\\{Shared.Constants.CompanyName}");
    }
}