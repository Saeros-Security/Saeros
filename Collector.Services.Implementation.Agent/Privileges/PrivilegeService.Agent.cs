using System.Diagnostics;
using Collector.Services.Implementation.Agent.Privileges.Native;
using Microsoft.Extensions.Logging;

namespace Collector.Services.Implementation.Agent.Privileges;

public sealed class PrivilegeServiceAgent(ILogger<PrivilegeServiceAgent> logger) : PrivilegeService
{
    private void SetLocalPrivileges()
    {
        using var process = Process.GetCurrentProcess();
        var privileges = process.GetPrivileges();
        var maxPrivilegeLength = privileges.Max(privilege => privilege.Privilege.ToString().Length);
        var currentSecurityState = process.GetPrivilegeState(Privilege.Security);
        // Privileges can only be enabled on a process if they are disabled.
        if (currentSecurityState == PrivilegeState.Disabled)
        {
            var result = process.EnablePrivilege(Privilege.Security);
            var securityState = process.GetPrivilegeState(Privilege.Security);
            logger.LogInformation(
                "{0}{1} => {2} ({3})",
                Privilege.Security,
                GetPadding(Privilege.Security.ToString().Length, maxPrivilegeLength),
                securityState,
                result);
        }
        else if (currentSecurityState == PrivilegeState.Removed)
        {
            logger.LogWarning("The Security privilege is removed from the current user");
        }

        var currentAuditState = process.GetPrivilegeState(Privilege.Audit);
        // Privileges can only be enabled on a process if they are disabled.
        if (currentAuditState == PrivilegeState.Disabled)
        {
            var result = process.EnablePrivilege(Privilege.Audit);
            var auditState = process.GetPrivilegeState(Privilege.Audit);
            logger.LogInformation(
                "{0}{1} => {2} ({3})",
                Privilege.Audit,
                GetPadding(Privilege.Audit.ToString().Length, maxPrivilegeLength),
                auditState,
                result);
        }
        else if (currentAuditState == PrivilegeState.Removed)
        {
            logger.LogWarning("The Audit privilege is removed from the current user");
        }

        var currentDebugState = process.GetPrivilegeState(Privilege.Debug);
        // Privileges can only be enabled on a process if they are disabled.
        if (currentDebugState == PrivilegeState.Disabled)
        {
            var result = process.EnablePrivilege(Privilege.Debug);
            var debugState = process.GetPrivilegeState(Privilege.Debug);
            logger.LogInformation(
                "{0}{1} => {2} ({3})",
                Privilege.Debug,
                GetPadding(Privilege.Debug.ToString().Length, maxPrivilegeLength),
                debugState,
                result);
        }
        else if (currentDebugState == PrivilegeState.Removed)
        {
            logger.LogWarning("The Debug privilege is removed from the current user");
        }
        
        var currentShutdownState = process.GetPrivilegeState(Privilege.Shutdown);
        // Privileges can only be enabled on a process if they are disabled.
        if (currentShutdownState == PrivilegeState.Disabled)
        {
            var result = process.EnablePrivilege(Privilege.Shutdown);
            var debugState = process.GetPrivilegeState(Privilege.Shutdown);
            logger.LogInformation(
                "{0}{1} => {2} ({3})",
                Privilege.Shutdown,
                GetPadding(Privilege.Shutdown.ToString().Length, maxPrivilegeLength),
                debugState,
                result);
        }
        else if (currentShutdownState == PrivilegeState.Removed)
        {
            logger.LogWarning("The Shutdown privilege is removed from the current user");
        }

        return;

        static string GetPadding(int length, int maxLength)
        {
            var paddingLength = maxLength - length;
            char[] padding = new char[paddingLength];
            for (var i = 0; i < paddingLength; i++)
            {
                padding[i] = ' ';
            }

            return new string(padding);
        }
    }
    
    public override void SetPrivileges()
    {
        SetLocalPrivileges();
    }
}