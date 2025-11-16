using System.Runtime.Versioning;
using System.Text;
using Collector.ActiveDirectory.AuditPolicies;
using Collector.ActiveDirectory.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Shared;
using Shared.Helpers;
using static Vanara.PInvoke.AdvApi32;

namespace Collector.ActiveDirectory.Helpers.AuditPolicies;

[SupportedOSPlatform("windows")]
public static class LocalAuditPolicyHelper
{
    public static async Task<byte[]> BackupAuditPoliciesAsync(ILogger logger, CancellationToken cancellationToken)
    {
        const string backupAuditPolicyFileName = "AuditPolicies.csv";
        var backupFile = Path.Join(Path.GetTempPath(), backupAuditPolicyFileName);
        if (File.Exists(backupFile))
        {
            File.Delete(backupFile);
        }

        var success = await ProcessHelper.RunAsync(onError: _ => logger.LogError("Could not backup Audit Policies"), processName: "auditpol.exe", arguments: $"/backup /file:{backupFile}", cancellationToken);
        if (success)
        {
            logger.LogInformation("Audit Policies have been successfully backup up");
            return await File.ReadAllBytesAsync(backupFile, cancellationToken);
        }

        return [];
    }

    public static async Task RestoreAuditPoliciesAsync(ILogger logger, byte[] backup, CancellationToken cancellationToken)
    {
        const string backupAuditPolicyFileName = "AuditPolicies.csv";
        var backupFile = Path.Join(Path.GetTempPath(), backupAuditPolicyFileName);
        if (File.Exists(backupFile))
        {
            File.Delete(backupFile);
        }

        await File.WriteAllBytesAsync(backupFile, backup, cancellationToken);
        var success = await ProcessHelper.RunAsync(onError: _ => logger.LogError("Could not restore Audit Policies"), processName: "auditpol.exe", arguments: $"/restore /file:{backupFile}", cancellationToken);
        if (success)
        {
            logger.LogInformation("Audit Policies have been successfully restored");
        }
    }

    public static void DeleteAuditPolicies(ILogger logger)
    {
        try
        {
            var syspath = EnvironmentVariableHelper.GetSystemPath();
            var localAuditPoliciesFileName = $"{syspath}\\system32\\GroupPolicy\\Machine\\Microsoft\\Windows NT\\Audit\\Audit.csv";
            var globalAuditPoliciesFileName = $"{syspath}\\security\\Audit\\Audit.csv";
            if (File.Exists(localAuditPoliciesFileName))
            {
                File.Delete(localAuditPoliciesFileName);
            }

            if (File.Exists(globalAuditPoliciesFileName))
            {
                File.Delete(globalAuditPoliciesFileName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not delete Audit Policies");
        }
    }

    public static async Task ClearAuditPoliciesAsync(ILogger logger, CancellationToken cancellationToken)
    {
        var success = await ProcessHelper.RunAsync(onError: error => logger.LogError("Could not clear Audit Policies. Error Code: {ExitCode}", error), processName: "auditpol.exe", arguments: "/clear /y", cancellationToken);
        if (success)
        {
            logger.LogInformation("Audit Policies have been cleared");
        }
    }

    public static async Task SetSubCategoryAuditOptionsAsync(ILogger logger, Guid subcategory, POLICY_AUDIT_EVENT_OPTIONS policyAuditEventOptions, CancellationToken cancellationToken)
    {
        if (policyAuditEventOptions == POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_UNCHANGED) return;

        var arguments = new List<string>
        {
            "/set",
            $"/subcategory:{{{subcategory}}}"
        };

        arguments.AddRange(BuildPolicyAuditOptions(policyAuditEventOptions));
        var success = await ProcessHelper.RunAsync(onError: error => logger.LogError("Could not set Audit Policies for {SubCategory}. Error Code: {ExitCode}", subcategory, error), processName: "auditpol.exe", arguments, cancellationToken);
        if (success && AuditLookupSubCategoryName(subcategory, out var subCategoryDisplayName))
        {
            var option = policyAuditEventOptions.Stringify();
            logger.LogInformation(
                "{0} => {1} (Volume: {2})",
                $"{subCategoryDisplayName,-50}",
                option,
                IsHighVolume(subcategory) ? "High" : "Standard");
        }
    }

    private static bool IsHighVolume(Guid subcategoryGuid)
    {
        if (AuditPolicyMapping.VolumeBySubcategoryGuid.TryGetValue(subcategoryGuid, out var volume))
        {
            if (volume is AuditPolicyVolume.High or AuditPolicyVolume.VeryHigh)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> BuildPolicyAuditOptions(POLICY_AUDIT_EVENT_OPTIONS policyAuditEventOptions)
    {
        var success = new StringBuilder();
        var failure = new StringBuilder();
        if (policyAuditEventOptions.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_NONE))
        {
            success.Append("/success:disable");
            failure.Append("/failure:disable");
        }
        else if (policyAuditEventOptions.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS) && policyAuditEventOptions.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE))
        {
            success.Append("/success:enable");
            failure.Append("/failure:enable");
        }
        else if (policyAuditEventOptions.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS) && !policyAuditEventOptions.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE))
        {
            success.Append("/success:enable");
            failure.Append("/failure:disable");
        }
        else if (!policyAuditEventOptions.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_SUCCESS) && policyAuditEventOptions.HasFlag(POLICY_AUDIT_EVENT_OPTIONS.POLICY_AUDIT_EVENT_FAILURE))
        {
            success.Append("/success:disable");
            failure.Append("/failure:enable");
        }

        return [success.ToString(), failure.ToString()];
    }

    public static bool IsAdvancedPoliciesEnabled(ILogger logger)
    {
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = localMachine.OpenSubKey(@"System\CurrentControlSet\Control\LSA");
            if (key?.GetValue("SCENoApplyLegacyAuditPolicy") is int value)
            {
                return value == 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not determine if Advanced Audit Policies are enabled");
        }

        return false;
    }

    public static void SetAdvancedPoliciesState(ILogger logger, bool enable)
    {
        const string key = "SCENoApplyLegacyAuditPolicy";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"System\CurrentControlSet\Control\LSA", writable: true);
            subKey?.SetValue(key, enable ? 1 : 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }

    public static void SetAuditReceivingNtlmTraffic(ILogger logger)
    {
        const string key = "AuditReceivingNTLMTraffic";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"System\\CurrentControlSet\\Control\\Lsa\\MSV1_0", writable: true);
            subKey?.SetValue(key, 2, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }

    public static void SetRestrictSendingNtlmTraffic(ILogger logger)
    {
        const string key = "RestrictSendingNTLMTraffic";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"System\\CurrentControlSet\\Control\\Lsa\\MSV1_0", writable: true);
            subKey?.SetValue(key, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }

    public static void SetAuditNtlmInDomain(ILogger logger)
    {
        const string key = "AuditNTLMInDomain";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"CurrentControlSet\services\Netlogon\Parameters", writable: true);
            subKey?.SetValue(key, 7, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }

    public static void EnableModuleLogging64(ILogger logger)
    {
        const string key = "EnableModuleLogging";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell", writable: true);
            using var moduleLogging = subKey?.CreateSubKey("ModuleLogging", writable: true);
            moduleLogging?.SetValue(key, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }
    
    public static void EnableModuleLogging32(ILogger logger)
    {
        const string key = "EnableModuleLogging";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\PowerShell", writable: true);
            using var moduleLogging = subKey?.CreateSubKey("ModuleLogging", writable: true);
            moduleLogging?.SetValue(key, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }
    
    public static void SetModuleNames64(ILogger logger)
    {
        const string key = "^*";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell", writable: true);
            using var moduleLogging = subKey?.CreateSubKey("ModuleLogging", writable: true);
            using var moduleNames = moduleLogging?.CreateSubKey("ModuleNames", writable: true);
            moduleNames?.SetValue(key, "^*", RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }
    
    public static void SetModuleNames32(ILogger logger)
    {
        const string key = "^*";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\PowerShell", writable: true);
            using var moduleLogging = subKey?.CreateSubKey("ModuleLogging", writable: true);
            using var moduleNames = moduleLogging?.CreateSubKey("ModuleNames", writable: true);
            moduleNames?.SetValue(key, "^*", RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }
    
    public static void EnableScriptBlockLogging64(ILogger logger)
    {
        const string key = "EnableScriptBlockLogging";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\PowerShell", writable: true);
            using var scriptBlockLogging = subKey?.CreateSubKey("ScriptBlockLogging", writable: true);
            scriptBlockLogging?.SetValue(key, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }
    
    public static void EnableScriptBlockLogging32(ILogger logger)
    {
        const string key = "EnableScriptBlockLogging";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\PowerShell", writable: true);
            using var scriptBlockLogging = subKey?.CreateSubKey("ScriptBlockLogging", writable: true);
            scriptBlockLogging?.SetValue(key, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }
    
    public static void EnableProcessCreationIncludeCmdLine(ILogger logger)
    {
        const string key = "ProcessCreationIncludeCmdLine_Enabled";
        try
        {
            using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\Audit", writable: true);
            subKey?.SetValue(key, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not set {key}");
        }
    }
}