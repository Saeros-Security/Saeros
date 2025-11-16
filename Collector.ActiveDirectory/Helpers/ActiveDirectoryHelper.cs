using System.Diagnostics.CodeAnalysis;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Helpers;
using Vanara.PInvoke;
using WmiLight;

namespace Collector.ActiveDirectory.Helpers;

public static class ActiveDirectoryHelper
{
    private static readonly Regex DistinguishedNameRegex = new("(CN=)(.*?),.*", RegexOptions.Compiled);
    
    public static bool TestConnection(ILogger logger, string domainName, string primaryDomainController, int ldapPort, string userName, string password, [MaybeNullWhen(true)] out string message)
    {
        message = null;
        try
        {
            using var connection = ActiveDirectoryManagement.CreateConnection(logger, new LdapDirectoryIdentifier(primaryDomainController, ldapPort), new NetworkCredential(userName, password, domainName));
            if (ActiveDirectoryManagement.TryGetNamingContext(logger, connection, out var rootNamingContext))
            {
                if (TryGetDistinguishedName(logger, rootNamingContext, objectSid: "S-1-5-32-544", connection, out var administratorsDistinguishedName))
                {
                    var searchRequest = new SearchRequest(rootNamingContext, ldapFilter: $"(&(objectClass=user)(memberOf:1.2.840.113556.1.4.1941:={administratorsDistinguishedName}))", SearchScope.Subtree, attributeList: ActiveDirectoryManagement.DistinguishedNameAttribute);
                    searchRequest.Controls.AddRange(ActiveDirectoryManagement.Controls);
                    if (ActiveDirectoryManagement.SendRequest(logger, searchRequest, connection) is not SearchResponse searchResponse) throw new Exception("Could not send search request");
                    if (searchResponse.ResultCode == ResultCode.Success)
                    {
                        var distinguishedNames = searchResponse.Entries.Cast<SearchResultEntry>().Where(entry => entry.Attributes.Contains(ActiveDirectoryManagement.DistinguishedNameAttribute)).SelectMany(e => e.Attributes[ActiveDirectoryManagement.DistinguishedNameAttribute].GetValues(typeof(string)).OfType<string>());
                        var memberOfDomainAdministrator = distinguishedNames.Any(dn => DistinguishedNameRegex.Match(dn).Groups[2].Value.Equals(userName, StringComparison.OrdinalIgnoreCase));
                        if (!memberOfDomainAdministrator)
                        {
                            message = $"The user {userName} is not a member of Administrators group";
                            return false;
                        }

                        return true;
                    }
                }

                message = "Could not find builtin Administrators group";
                return false;
            }

            message = "Could not retrieve naming context of the domain";
            return false;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            logger.LogError(ex, "An error has occurred");
            return false;
        }
    }
    
    private static bool TryGetDistinguishedName(ILogger logger, string rootNamingContext, string objectSid, LdapConnection connection, [MaybeNullWhen(false)] out string distinguishedName)
    {
        distinguishedName = null;
        var searchRequest = new SearchRequest(rootNamingContext, ldapFilter: $"(objectSid={objectSid})", SearchScope.Subtree, attributeList: ActiveDirectoryManagement.DistinguishedNameAttribute);
        searchRequest.Controls.AddRange(ActiveDirectoryManagement.Controls);
        if (ActiveDirectoryManagement.SendRequest(logger, searchRequest, connection) is not SearchResponse searchResponse) throw new Exception("Could not send search request");
        if (searchResponse.ResultCode == ResultCode.Success)
        {
            var distinguishedNames = searchResponse.Entries.Cast<SearchResultEntry>().Where(entry => entry.Attributes.Contains(ActiveDirectoryManagement.DistinguishedNameAttribute)).SelectMany(e => e.Attributes[ActiveDirectoryManagement.DistinguishedNameAttribute].GetValues(typeof(string)).OfType<string>());
            distinguishedName = distinguishedNames.SingleOrDefault();
        }

        return !string.IsNullOrEmpty(distinguishedName);
    }

    public static string GetPrimaryDomainControllerDnsName(ILogger logger, string domain, CancellationToken cancellationToken)
    {
        var policy = Policy<string>.Handle<Exception>(e => e is not OperationCanceledException).WaitAndRetryForever(_ => TimeSpan.FromSeconds(5), onRetry: (_, next) => { logger.LogWarning("Could not fetch primary domain controller. Retrying in '{Retry}s'...", next.TotalSeconds); });
        return policy.Execute(ct =>
        {
            ct.ThrowIfCancellationRequested();
            return NetApi32.DsGetDcEnum(domain, DcFlags: NetApi32.DsGetDcNameFlags.DS_PDC_REQUIRED).Select(domainController => domainController.dnsHostName).First(dnsHostName => !string.IsNullOrWhiteSpace(dnsHostName))!;
        }, cancellationToken);
    }

    public static IEnumerable<(string serverName, string ipAddress)> EnumerateDomainControllers(ILogger logger, string domain, CancellationToken cancellationToken)
    {
        var policy = Policy<IEnumerable<(string serverName, string ipAddress)>>.Handle<Exception>(e => e is not OperationCanceledException).WaitAndRetryForever(_ => TimeSpan.FromSeconds(5), onRetry: (_, next) => { logger.LogWarning("Could not enumerate domain controllers. Retrying in '{Retry}s'...", next.TotalSeconds); });
        return policy.Execute(ct =>
        {
            ct.ThrowIfCancellationRequested();
            var domainControllers = new List<(string serverName, string ipAddress)>();
            foreach (var server in NetApi32.DsGetDcEnum(domain))
            {
                if (string.IsNullOrWhiteSpace(server.dnsHostName)) continue;
                if (TryGetIpAddress(server.dnsHostName, domain, out var ipAddress))
                {
                    domainControllers.Add((server.dnsHostName, ipAddress));
                }
            }

            return domainControllers;
        }, cancellationToken);
    }

    private static bool TryGetIpAddress(string serverName, string domain, [MaybeNullWhen(false)] out string ipAddress)
    {
        ipAddress = null;
        try
        {
            var info = NetApi32.DsGetDcName(NetApi32.DsGetDcNameFlags.DS_RETURN_DNS_NAME, serverName, domain);
            ipAddress = info.DomainControllerAddress.Replace(@"\\", string.Empty);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void UpdateGroupPolicies(ILogger logger, string primaryDomainController, CancellationToken cancellationToken)
    {
        try
        {
            if (MachineNameHelper.FullyQualifiedName.Equals(primaryDomainController, StringComparison.OrdinalIgnoreCase))
            {
                var success = ProcessHelper.Run(onError: error => logger.LogWarning("Could not invoke GPUpdate on {PrimaryDomainControllerDnsName}: {Code}", primaryDomainController, error), processName: "GPUpdate.exe", arguments: "/target:computer /force", cancellationToken);
                if (success)
                {
                    logger.LogInformation("Successfully invoked GPUpdate on {PrimaryDomainControllerDnsName}", primaryDomainController);
                }

                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(1));
            var opt = new WmiConnectionOptions { EnablePackageEncryption = true };
            using var connection = new WmiConnection($@"\\{primaryDomainController}\root\cimv2", opt);
            using var createMethod = connection.GetMethod("Win32_Process", "Create");
            using var methodParams = createMethod.CreateInParameters();
            methodParams.SetPropertyValue("CommandLine", "cmd.exe /c GPUpdate.exe /target:computer /force");
            var code = connection.ExecuteMethod<uint>(createMethod, methodParams, out var outParameters);
            if (code == 0)
            {
                var pid = (uint)outParameters["processId"];
                while (!cts.IsCancellationRequested)
                {
                    if (connection.CreateQuery("SELECT * FROM Win32_Process").All(process => process.GetPropertyValue<uint>("processId") != pid))
                    {
                        logger.LogInformation("Successfully invoked GPUpdate on {PrimaryDomainControllerDnsName}", primaryDomainController);
                        return;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }

            logger.LogWarning("Could not invoke GPUpdate on {PrimaryDomainControllerDnsName}: {Code}", primaryDomainController, code);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An error has occurred");
        }
    }

    public static async Task UpdateGroupPoliciesAsync(ILogger logger, string primaryDomainController, CancellationToken cancellationToken)
    {
        try
        {
            if (MachineNameHelper.FullyQualifiedName.Equals(primaryDomainController, StringComparison.OrdinalIgnoreCase))
            {
                var success = await ProcessHelper.RunAsync(onError: error => logger.LogWarning("Could not invoke GPUpdate on {PrimaryDomainControllerDnsName}: {Code}", primaryDomainController, error), processName: "GPUpdate.exe", arguments: "/target:computer /force", cancellationToken);
                if (success)
                {
                    logger.LogInformation("Successfully invoked GPUpdate on {PrimaryDomainControllerDnsName}", primaryDomainController);
                }

                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(1));
            var opt = new WmiConnectionOptions { EnablePackageEncryption = true };
            using var connection = new WmiConnection($@"\\{primaryDomainController}\root\cimv2", opt);
            using var createMethod = connection.GetMethod("Win32_Process", "Create");
            using var methodParams = createMethod.CreateInParameters();
            methodParams.SetPropertyValue("CommandLine", "cmd.exe /c GPUpdate.exe /target:computer /force");
            var code = connection.ExecuteMethod<uint>(createMethod, methodParams, out var outParameters);
            if (code == 0)
            {
                var pid = (uint)outParameters["processId"];
                while (!cts.IsCancellationRequested)
                {
                    if (connection.CreateQuery("SELECT * FROM Win32_Process").All(process => process.GetPropertyValue<uint>("processId") != pid))
                    {
                        logger.LogInformation("Successfully invoked GPUpdate on {PrimaryDomainControllerDnsName}", primaryDomainController);
                        return;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                }
            }

            logger.LogWarning("Could not invoke GPUpdate on {PrimaryDomainControllerDnsName}: {Code}", primaryDomainController, code);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An error has occurred");
        }
    }

    public static async Task UpdateGroupPoliciesAsync(ILogger logger, string primaryDomainController, NetworkCredential credential, CancellationToken cancellationToken)
    {
        try
        {
            if (MachineNameHelper.FullyQualifiedName.Equals(primaryDomainController, StringComparison.OrdinalIgnoreCase))
            {
                var success = await ProcessHelper.RunAsync(onError: error => logger.LogWarning("Could not invoke GPUpdate on {PrimaryDomainControllerDnsName}: {Code}", primaryDomainController, error), processName: "GPUpdate.exe", arguments: "/target:computer /force", cancellationToken);
                if (success)
                {
                    logger.LogInformation("Successfully invoked GPUpdate on {PrimaryDomainControllerDnsName}", primaryDomainController);
                }

                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(1));
            var opt = new WmiConnectionOptions { EnablePackageEncryption = true };
            using var connection = new WmiConnection($@"\\{primaryDomainController}\root\cimv2", credential, opt);
            using var createMethod = connection.GetMethod("Win32_Process", "Create");
            using var methodParams = createMethod.CreateInParameters();
            methodParams.SetPropertyValue("CommandLine", "cmd.exe /c GPUpdate.exe /target:computer /force");
            var code = connection.ExecuteMethod<uint>(createMethod, methodParams, out var outParameters);
            if (code == 0)
            {
                var pid = (uint)outParameters["processId"];
                while (!cts.IsCancellationRequested)
                {
                    if (connection.CreateQuery("SELECT * FROM Win32_Process").All(process => process.GetPropertyValue<uint>("processId") != pid))
                    {
                        logger.LogInformation("Successfully invoked GPUpdate on {PrimaryDomainControllerDnsName}", primaryDomainController);
                        return;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                }
            }

            logger.LogWarning("Could not invoke GPUpdate on {PrimaryDomainControllerDnsName}: {Code}", primaryDomainController, code);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An error has occurred");
        }
    }
    
    public static async Task InvokeScheduledTaskAsync(ILogger logger, ScheduledTasks.ScheduledTasksHelper.ScheduleTaskType type, string primaryDomainController, NetworkCredential credential, CancellationToken cancellationToken)
    {
        try
        {
            var taskName = type switch
            {
                ScheduledTasks.ScheduledTasksHelper.ScheduleTaskType.ServiceCreation => ScheduledTasks.ScheduledTasksHelper.ServiceCreationTaskName,
                _ => string.Empty
            };

            if (MachineNameHelper.FullyQualifiedName.Equals(primaryDomainController, StringComparison.OrdinalIgnoreCase))
            {
                var success = await ProcessHelper.RunAsync(onError: error => logger.LogWarning("Could not invoke SCHTASKS on {PrimaryDomainControllerDnsName}: {Code}", primaryDomainController, error), processName: "SCHTASKS.exe", arguments: $"/Run /tn \"{taskName}\"", cancellationToken);
                if (success)
                {
                    logger.LogInformation("Successfully invoked SCHTASKS on {PrimaryDomainControllerDnsName}", primaryDomainController);
                }

                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(1));
            var opt = new WmiConnectionOptions { EnablePackageEncryption = true };
            using var connection = new WmiConnection($@"\\{primaryDomainController}\root\cimv2", credential, opt);
            using var createMethod = connection.GetMethod("Win32_Process", "Create");
            using var methodParams = createMethod.CreateInParameters();
            methodParams.SetPropertyValue("CommandLine", $"cmd.exe /c SCHTASKS.exe /Run /tn \"{taskName}\"");
            var code = connection.ExecuteMethod<uint>(createMethod, methodParams, out var outParameters);
            if (code == 0)
            {
                var pid = (uint)outParameters["processId"];
                while (!cts.IsCancellationRequested)
                {
                    if (connection.CreateQuery("SELECT * FROM Win32_Process").All(process => process.GetPropertyValue<uint>("processId") != pid))
                    {
                        logger.LogInformation("Successfully invoked SCHTASKS on {PrimaryDomainControllerDnsName}", primaryDomainController);
                        return;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                }
            }

            logger.LogWarning("Could not invoke SCHTASKS on {PrimaryDomainControllerDnsName}: {Code}", primaryDomainController, code);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An error has occurred");
        }
    }
    
    public static async Task DeleteScheduledTaskAsync(ILogger logger, ScheduledTasks.ScheduledTasksHelper.ScheduleTaskType type, CancellationToken cancellationToken)
    {
        try
        {
            var taskName = type switch
            {
                ScheduledTasks.ScheduledTasksHelper.ScheduleTaskType.ServiceCreation => ScheduledTasks.ScheduledTasksHelper.ServiceCreationTaskName,
                _ => string.Empty
            };

            var success = await ProcessHelper.RunAsync(onError: error => logger.LogWarning("Could not delete SCHTASKS: {Code}", error), processName: "SCHTASKS.exe", arguments: $"/delete /tn \"{taskName}\" /f", cancellationToken);
            if (success)
            {
                logger.LogInformation("Successfully deleted SCHTASKS");
                return;
            }

            logger.LogWarning("Could not delete SCHTASKS");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An error has occurred");
        }
    }
}