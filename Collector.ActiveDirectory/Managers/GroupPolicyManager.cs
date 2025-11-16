using System.Diagnostics.CodeAnalysis;
using System.DirectoryServices.Protocols;
using System.Text;
using Collector.ActiveDirectory.Clients;
using Collector.ActiveDirectory.Helpers.RegistryPol;
using Collector.ActiveDirectory.Helpers.ScheduledTasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Shared;
using Shared.Extensions;
using SearchOption = System.IO.SearchOption;

namespace Collector.ActiveDirectory.Managers;

public sealed class GroupPolicyManager : ActiveDirectoryManagement
{
    public GroupPolicyManager(string domain, string username, string password, string server, int ldapPort, ILogger logger) : base(domain, username, password, server, ldapPort, logger)
    {
        GpoPath = $@"\\{NetworkCredential.Domain}\sysvol\{NetworkCredential.Domain}\Policies\{GroupPolicyObjectGuid.ToString("B").ToUpper()}";
    }

    private const string GpLinkAttribute = "gPLink";
    private static readonly string GpoName = Constants.CompanyName;
    private static readonly RetryPolicy LdapPolicy = Policy.Handle<LdapException>().WaitAndRetry(3, sleepDurationProvider: _ => TimeSpan.FromSeconds(1));
    
    public static readonly Guid GroupPolicyObjectGuid = new("3560ff19-45a3-4f9a-956b-937a04d2aabf");
    public static readonly string DeleteFileName = "Delete.txt";
    public static readonly string CollectorServiceDisplayName = $"{Constants.CompanyName} {Constants.CollectorName} ({Enum.GetName(CollectorMode.Agent)})";
    public static readonly string CollectorServiceName = $"{Constants.CompanyName}_{Constants.CollectorName}_{Enum.GetName(CollectorMode.Agent)}";
    public static readonly string CollectorServiceFileName = $"{Constants.CollectorName}.exe";
    
    private string GpoPath { get; }
    
    public void RemoveGroupPolicyObject()
    {
        Logger.LogInformation("The group policy object will be uninstalled from {Path}", GpoPath);
        if (ObjectExists(GroupPolicyObjectGuid))
        {
            using var smbClient = new SmbClient(Logger, LdapDirectoryIdentifier.Servers.Single(), NetworkCredential.Domain, NetworkCredential.UserName, NetworkCredential.Password);
            RemoveGroupPolicyObjectCore(smbClient);
        }
    }

    private void RemoveGroupPolicyObjectCore(SmbClient smbClient)
    {
        WriteDeletionFile(smbClient, GpoPath);
        UnLinkGroupPolicyObject(GroupPolicyObjectGuid, organizationalUnit: "OU=Domain Controllers");
        _ = DeleteContainer(GroupPolicyObjectGuid, container: "Machine");
        _ = DeleteContainer(GroupPolicyObjectGuid, container: "User");
        _ = DeleteGroupPolicyObject(GroupPolicyObjectGuid);
    }
    
    public void SetGroupPolicyObject()
    {
        var remoteRootPath = $@"\\{NetworkCredential.Domain}\sysvol\{NetworkCredential.Domain}\Policies\{GroupPolicyObjectGuid.ToString("B").ToUpper()}";
        Logger.LogInformation("The group policy object will be deployed to {Path}", remoteRootPath);
        using var smbClient = new SmbClient(Logger, LdapDirectoryIdentifier.Servers.Single(), NetworkCredential.Domain, NetworkCredential.UserName, NetworkCredential.Password);
        SetGroupPolicyObjectCore(smbClient, remoteRootPath);
    }

    private void SetGroupPolicyObjectCore(SmbClient smbClient, string remoteRootPath)
    {
        RemoveDeletionFile(smbClient, remoteRootPath);
        if (ObjectExists(GroupPolicyObjectGuid))
        {
            if (GptExists(smbClient, remoteRootPath))
            {
                AddUpdateFolder(smbClient, remoteRootPath);
                CreateScheduledTasks(smbClient, remoteRootPath, NetworkCredential.Domain);
                LinkGroupPolicyObject(GroupPolicyObjectGuid, organizationalUnit: "OU=Domain Controllers");
            }
            else
            {
                RemoveGroupPolicyObjectCore(smbClient);
                SetGroupPolicyObjectCore(smbClient, remoteRootPath);
            }
        }
        else
        {
            if (!CreateGroupPolicyObject(GpoName, GroupPolicyObjectGuid, remoteRootPath))
            {
                throw new Exception("Could not create the group policy object");
            }

            CopyExecutables(smbClient, CreateFolders(smbClient, remoteRootPath));
            CreateGpt(smbClient, remoteRootPath);
            CreateGptTmpl(smbClient, remoteRootPath);
            CreateRegistryXml(smbClient, remoteRootPath);
            CreateScheduledTasks(smbClient, remoteRootPath, NetworkCredential.Domain);
            CreateComments(smbClient, remoteRootPath);
            CreateRegistryPol(smbClient, remoteRootPath);
            LinkGroupPolicyObject(GroupPolicyObjectGuid, organizationalUnit: "OU=Domain Controllers");
        }
    }

    private bool ObjectExists(Guid groupPolicyObjectGuid)
    {
        return LdapPolicy.Execute(() => TryGetRootNamingContext(out var rootNamingContext) && TryFindObject(distinguishedName: $"CN={groupPolicyObjectGuid.ToString("B").ToUpper()},CN=Policies,CN=System,{rootNamingContext}", filter: "(objectClass=*)", SearchScope.Subtree));
    }

    private static void WriteDeletionFile(SmbClient smbClient, string rootPath)
    {
        var companyFolder = $@"{rootPath}\{Constants.CompanyName}";
        if (smbClient.DirectoryExists(companyFolder))
        {
            smbClient.WriteFile(content: "DELETE", Path.Join(companyFolder, DeleteFileName));
        }
    }

    private static void RemoveDeletionFile(SmbClient smbClient, string rootPath)
    {
        var companyFolder = $@"{rootPath}\{Constants.CompanyName}";
        if (smbClient.FileExists(Path.Join(companyFolder, DeleteFileName)))
        {
            smbClient.DeleteFile(Path.Join(companyFolder, DeleteFileName));
        }
    }

    private void AddUpdateFolder(SmbClient smbClient, string rootPath)
    {
        var updateFolder = $@"{rootPath}\{Constants.CompanyName}\{Guid.NewGuid():B}";
        using var collectorStream = File.OpenRead(Environment.GetCommandLineArgs().First());
        try
        {
            smbClient.CreateDirectory(updateFolder);
            smbClient.WriteFile(collectorStream, Path.Join(updateFolder, CollectorServiceFileName));
            foreach (var libraryPath in EnumerateLibraries())
            {
                using var libraryStream = File.OpenRead(libraryPath);
                smbClient.WriteFile(libraryStream, Path.Join(updateFolder, Path.GetFileName(libraryPath)));
            }

            const int retainedUpdateFolders = 2;
            var updateFolders = smbClient.EnumerateDirectories($@"{rootPath}\{Constants.CompanyName}");
            if (updateFolders.Count > retainedUpdateFolders)
            {
                foreach (var folder in updateFolders.Take(updateFolders.Count - retainedUpdateFolders))
                {
                    smbClient.DeleteFolder(folder);
                }
            }
        }
        catch (IOException ex) when (ex.Message.Contains("The process cannot access the file"))
        {
            Logger.LogError(ex, "Could not update collector");
        }
    }
    
    private bool CreateGroupPolicyObject(string name, Guid gpoGuid, string directory)
    {
        return LdapPolicy.Execute(() =>
        {
            if (TryGetRootNamingContext(out var rootNamingContext))
            {
                using var connection = GetConnection();
                var distinguishedName = $"CN={gpoGuid.ToString("B").ToUpper()},CN=Policies,CN=System,{rootNamingContext}";
                var addRequest = new AddRequest(distinguishedName);
                addRequest.Attributes.Add(new DirectoryAttribute("displayName", name));
                addRequest.Attributes.Add(new DirectoryAttribute("objectClass", "groupPolicyContainer"));
                addRequest.Attributes.Add(new DirectoryAttribute("flags", "0"));
                addRequest.Attributes.Add(new DirectoryAttribute("gPCFileSysPath", directory));
                addRequest.Attributes.Add(new DirectoryAttribute("versionNumber", "1"));
                addRequest.Attributes.Add(new DirectoryAttribute("gPCFunctionalityVersion", "2"));
                addRequest.Attributes.Add(new DirectoryAttribute("gPCMachineExtensionNames", "[{00000000-0000-0000-0000-000000000000}{BEE07A6A-EC9F-4659-B8C9-0B1937907C83}{CAB54552-DEEA-4691-817E-ED4A4D1AFC72}][{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{D02B1F72-3407-48AE-BA88-E8213C6761F1}][{827D319E-6EAC-11D2-A4EA-00C04F79F83A}{803E14A0-B4FB-11D0-A0D0-00A0C90F574B}][{AADCED64-746C-4633-A97C-D61349046527}{CAB54552-DEEA-4691-817E-ED4A4D1AFC72}][{B087BE9D-ED37-454F-AF9C-04291E351182}{BEE07A6A-EC9F-4659-B8C9-0B1937907C83}][{F3CCC681-B74C-4060-9F26-CD84525DCA2A}{0F3F3735-573D-9804-99E4-AB2A69BA5FD4}]"));
                var response = SendRequest(Logger, addRequest, connection);
                if (response.ResultCode == ResultCode.Success)
                {
                    if (ObjectExists(gpoGuid))
                    {
                        return CreateContainer(GroupPolicyObjectGuid, container: "Machine") && CreateContainer(GroupPolicyObjectGuid, container: "User");
                    }
                }
            }

            return false;
        });
    }
    
    private bool CreateContainer(Guid gpoGuid, string container)
    {
        return LdapPolicy.Execute(() =>
        {
            if (TryGetRootNamingContext(out var rootNamingContext))
            {
                using var connection = GetConnection();
                var distinguishedName = $"CN={container},CN={gpoGuid.ToString("B").ToUpper()},CN=Policies,CN=System,{rootNamingContext}";
                var addRequest = new AddRequest(distinguishedName);
                addRequest.Attributes.Add(new DirectoryAttribute("objectClass", "container"));
                var response = SendRequest(Logger, addRequest, connection);
                if (response.ResultCode == ResultCode.Success)
                {
                    return true;
                }
            }

            return false;
        });
    }

    private static string CreateFolders(SmbClient smbClient, string rootPath)
    {
        smbClient.CreateDirectory(rootPath);
        var machinePath = $@"{rootPath}\Machine";
        smbClient.CreateDirectory(machinePath);
        var scriptPath = $@"{machinePath}\Scripts";
        smbClient.CreateDirectory(scriptPath);
        var shutdownPath = $@"{scriptPath}\Shutdown";
        smbClient.CreateDirectory(shutdownPath);
        var startupPath = $@"{scriptPath}\Startup";
        smbClient.CreateDirectory(startupPath);
        var preferencesPath = $@"{machinePath}\Preferences";
        smbClient.CreateDirectory(preferencesPath);
        var registryPath = $@"{preferencesPath}\Registry";
        smbClient.CreateDirectory(registryPath);
        var scheduledTasksPath = $@"{preferencesPath}\ScheduledTasks";
        smbClient.CreateDirectory(scheduledTasksPath);
        var userPath = $@"{rootPath}\User";
        smbClient.CreateDirectory(userPath);
        smbClient.CreateDirectory($@"{rootPath}\{Constants.CompanyName}");
        return $@"{rootPath}\{Constants.CompanyName}";
    }

    private bool DeleteGroupPolicyObject(Guid gpoGuid)
    {
        return LdapPolicy.Execute(() =>
        {
            if (TryGetRootNamingContext(out var rootNamingContext))
            {
                using var connection = GetConnection();
                var distinguishedName = $"CN={gpoGuid.ToString("B").ToUpper()},CN=Policies,CN=System,{rootNamingContext}";
                var addRequest = new DeleteRequest(distinguishedName);
                var response = SendRequest(Logger, addRequest, connection);
                if (response.ResultCode == ResultCode.Success)
                {
                    return true;
                }
            }

            return false;
        });
    }

    private bool DeleteContainer(Guid gpoGuid, string container)
    {
        return LdapPolicy.Execute(() =>
        {
            if (TryGetRootNamingContext(out var rootNamingContext))
            {
                using var connection = GetConnection();
                var distinguishedName = $"CN={container},CN={gpoGuid.ToString("B").ToUpper()},CN=Policies,CN=System,{rootNamingContext}";
                var addRequest = new DeleteRequest(distinguishedName);
                var response = SendRequest(Logger, addRequest, connection);
                if (response.ResultCode == ResultCode.Success)
                {
                    return true;
                }
            }

            return false;
        });
    }

    private static void CopyExecutables(SmbClient smbClient, string path)
    {
        using var collectorStream = File.OpenRead(Environment.GetCommandLineArgs().First());
        smbClient.WriteFile(collectorStream, $"{Path.Join(path, CollectorServiceFileName)}");
        foreach (var libraryPath in EnumerateLibraries())
        {
            using var libraryStream = File.OpenRead(libraryPath);
            smbClient.WriteFile(libraryStream, Path.Join(path, Path.GetFileName(libraryPath)));
        }
    }
    
    private static void CreateGpt(SmbClient smbClient, string rootPath)
    {
        var iniPath = $@"{rootPath}\GPT.ini";
        const string iniContent = "[General]\r\nVersion=1\r\n";
        smbClient.WriteFile(iniContent, iniPath);
    }
    
    private static bool GptExists(SmbClient smbClient, string rootPath)
    {
        var iniPath = $@"{rootPath}\GPT.ini";
        return smbClient.FileExists(iniPath);
    }
    
    private static void CreateRegistryXml(SmbClient smbClient, string rootPath)
    {
        var registryPath = $@"{rootPath}\Machine\Preferences\Registry";
        var xmlPath = $@"{registryPath}\Registry.xml";
        string xmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RegistrySettings clsid=""{A3CCFC41-DFDB-43a5-8D26-0FE8B954DA51}""></RegistrySettings>";
        
        smbClient.CreateDirectory(registryPath);
        smbClient.WriteFile(xmlContent, xmlPath);
    }

    private void CreateGptTmpl(SmbClient smbClient, string rootPath)
    {
        var infPath = $@"{rootPath}\Machine\Microsoft\Windows NT\SecEdit\GptTmpl.inf";
        var infContent = @"[Unicode]
Unicode=yes
[Version]
signature=""$CHICAGO$""
Revision=1
";
        var secEditPath = $@"{rootPath}\Machine\Microsoft\Windows NT\SecEdit";
        smbClient.CreateDirectory(secEditPath);
        smbClient.WriteFile(infContent, infPath);
    }

    private static void CreateScheduledTasks(SmbClient smbClient, string rootPath, string domain)
    {
        var scheduledTasksFilePath = $@"{rootPath}\Machine\Preferences\ScheduledTasks\ScheduledTasks.xml";
        var scheduledTasksPath = $@"{rootPath}\Machine\Preferences\ScheduledTasks";
        smbClient.CreateDirectory(scheduledTasksPath);
        var scheduledTaskContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ScheduledTasks clsid=""{{CC63F200-7309-4ba0-B154-A71CD118DBCC}}"">
    <ImmediateTaskV2 clsid=""{{9756B581-76EC-4169-9AFC-0CA8D43ADB5F}}"" name=""{ScheduledTasksHelper.ServiceCreationTaskName}"" image=""0"" changed=""{DateTime.Now:yyyy-MM-dd HH:mm:ss}"" uid=""{ScheduledTasksHelper.ServiceCreationTaskName.ToGuid():B}""><Properties action=""C"" name=""{ScheduledTasksHelper.ServiceCreationTaskName}"" runAs=""NT AUTHORITY\System"" logonType=""S4U""><Task version=""1.3""><RegistrationInfo><Author>{Constants.CompanyName}</Author><Description>Immediate Task in charge of installing the {CollectorServiceDisplayName} service</Description></RegistrationInfo><Principals><Principal id=""Author""><UserId>NT AUTHORITY\System</UserId><LogonType>S4U</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals><Settings><IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>false</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession><UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine><WakeToRun>false</WakeToRun><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority><RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure></Settings><Triggers><TimeTrigger><Repetition><Interval>PT5M</Interval><StopAtDurationEnd>false</StopAtDurationEnd></Repetition><StartBoundary>%LocalTimeXmlEx%</StartBoundary><Enabled>true</Enabled></TimeTrigger></Triggers><Actions Context=""Author""><Exec><Command>cmd.exe</Command><Arguments>/c sc create {CollectorServiceName} displayname= ""{CollectorServiceDisplayName}"" binpath= ""\""%SystemRoot%\SYSVOL\sysvol\{domain}\Policies\{GroupPolicyObjectGuid:B}\{Constants.CompanyName}\{CollectorServiceFileName}\"" --mode {Constants.CollectorAgentMode}"" start= delayed-auto depend= LanmanWorkstation/KeyIso/Winmgmt/NTDS</Arguments></Exec><Exec><Command>cmd.exe</Command><Arguments>/c sc failure {CollectorServiceName} reset= 86400 actions= restart/1000/restart/5000/restart/10000</Arguments></Exec><Exec><Command>cmd.exe</Command><Arguments>/c sc description {CollectorServiceName} ""Service in charge of collecting security events""</Arguments></Exec><Exec><Command>cmd.exe</Command><Arguments>/c sc failureflag {CollectorServiceName} 1</Arguments></Exec><Exec><Command>cmd.exe</Command><Arguments>/c sc start {CollectorServiceName}</Arguments></Exec></Actions></Task></Properties></ImmediateTaskV2>
</ScheduledTasks>";
        smbClient.WriteFile(scheduledTaskContent, scheduledTasksFilePath);
    }

    private static void CreateComments(SmbClient smbClient, string rootPath)
    {
        var commentFilePath = $@"{rootPath}\Machine\comment.cmtx";
        const string commentContent = "77u/PD94bWwgdmVyc2lvbj0nMS4wJyBlbmNvZGluZz0ndXRmLTgnPz4NCjxwb2xpY3lDb21tZW50cyB4bWxuczp4c2Q9Imh0dHA6Ly93d3cudzMub3JnLzIwMDEvWE1MU2NoZW1hIiB4bWxuczp4c2k9Imh0dHA6Ly93d3cudzMub3JnLzIwMDEvWE1MU2NoZW1hLWluc3RhbmNlIiByZXZpc2lvbj0iMS4wIiBzY2hlbWFWZXJzaW9uPSIxLjAiIHhtbG5zPSJodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vR3JvdXBQb2xpY3kvQ29tbWVudERlZmluaXRpb25zIj4NCiAgPHBvbGljeU5hbWVzcGFjZXM+DQogICAgPHVzaW5nIHByZWZpeD0ibnMwIiBuYW1lc3BhY2U9Ik1pY3Jvc29mdC5Qb2xpY2llcy5Hcm91cFBvbGljeSI+PC91c2luZz4NCiAgPC9wb2xpY3lOYW1lc3BhY2VzPg0KICA8Y29tbWVudHM+DQogICAgPGFkbVRlbXBsYXRlPjwvYWRtVGVtcGxhdGU+DQogIDwvY29tbWVudHM+DQogIDxyZXNvdXJjZXMgbWluUmVxdWlyZWRSZXZpc2lvbj0iMS4wIj4NCiAgICA8c3RyaW5nVGFibGU+PC9zdHJpbmdUYWJsZT4NCiAgPC9yZXNvdXJjZXM+DQo8L3BvbGljeUNvbW1lbnRzPg==";
        smbClient.WriteFile(Convert.FromBase64String(commentContent), commentFilePath);
    }

    private static void CreateRegistryPol(SmbClient smbClient, string rootPath)
    {
        var registryPolFilePath = $@"{rootPath}\Machine\Registry.pol";
        smbClient.WriteFile(CreateRegistryPol(), registryPolFilePath);
    }
    
    private bool TryGetGpLink(string distinguishedName, [MaybeNullWhen(false)] out string gPLink)
    {
        gPLink = null;
        using var connection = GetConnection();
        var searchRequest = new SearchRequest(distinguishedName, ldapFilter: "(objectClass=*)", SearchScope.Base, GpLinkAttribute);
        searchRequest.Controls.AddRange(Controls);
        if (SendRequest(Logger, searchRequest, connection) is not SearchResponse searchResponse) throw new Exception("Could not send search request");
        if (searchResponse.ResultCode == ResultCode.NoSuchObject)
        {
            return false;
        }

        if (searchResponse.ResultCode == ResultCode.Success)
        {
            var entries = searchResponse.Entries.Cast<SearchResultEntry>().ToList();
            if (entries.All(e => e.Attributes.Count == 0)) return false;
            var entry = entries.Select(entry => entry.Attributes[GpLinkAttribute].Cast<byte[]>().Single()).Single();
            gPLink = Encoding.UTF8.GetString(entry);
            return true;
        }

        throw new LdapException((int)searchResponse.ResultCode, searchResponse.ErrorMessage);
    }

    private void LinkGroupPolicyObject(Guid groupPolicyObjectGuid, string organizationalUnit)
    {
        LdapPolicy.Execute(() =>
        {
            if (TryGetRootNamingContext(out var rootNamingContext))
            {
                var distinguishedName = $"{organizationalUnit},{rootNamingContext}";
                if (TryGetGpLink(distinguishedName, out var gPLink))
                {
                    var guid = groupPolicyObjectGuid.ToString("B").ToUpper();
                    if (gPLink.Contains(StandardizeGuid(guid), StringComparison.OrdinalIgnoreCase)) return;
                    using var connection = GetConnection();
                    var modifyRequest = new ModifyRequest(distinguishedName: distinguishedName);
                    var directoryAttributeModification = new DirectoryAttributeModification
                    {
                        Name = GpLinkAttribute,
                        Operation = DirectoryAttributeOperation.Replace
                    };

                    var groupPolicyObjectDistinguishedName = $"CN={guid},CN=Policies,CN=System,{rootNamingContext}";
                    directoryAttributeModification.Add($"[LDAP://{groupPolicyObjectDistinguishedName};2]{gPLink}");
                    modifyRequest.Modifications.Add(directoryAttributeModification);
                    var response = SendRequest(Logger, modifyRequest, connection);
                    if (response.ResultCode != ResultCode.Success)
                    {
                        throw new LdapException((int)response.ResultCode, response.ErrorMessage);
                    }
                }
                else
                {
                    var guid = groupPolicyObjectGuid.ToString("B").ToUpper();
                    using var connection = GetConnection();
                    var addRequest = new AddRequest(distinguishedName: distinguishedName);
                    var groupPolicyObjectDistinguishedName = $"CN={guid},CN=Policies,CN=System,{rootNamingContext}";
                    addRequest.Attributes.Add(new DirectoryAttribute(GpLinkAttribute, $"[LDAP://{groupPolicyObjectDistinguishedName};2]"));
                    var response = SendRequest(Logger, addRequest, connection);
                    if (response.ResultCode != ResultCode.Success)
                    {
                        throw new LdapException((int)response.ResultCode, response.ErrorMessage);
                    }
                }
            }
        });
    }
    
    private void UnLinkGroupPolicyObject(Guid groupPolicyObjectGuid, string organizationalUnit)
    {
        LdapPolicy.Execute(() =>
        {
            if (TryGetRootNamingContext(out var rootNamingContext))
            {
                var distinguishedName = $"{organizationalUnit},{rootNamingContext}";
                if (TryGetGpLink(distinguishedName, out var gPLink))
                {
                    var guid = groupPolicyObjectGuid.ToString("B").ToUpper();
                    if (!gPLink.Contains(StandardizeGuid(guid), StringComparison.OrdinalIgnoreCase)) return;
                    using var connection = GetConnection();
                    var modifyRequest = new ModifyRequest(distinguishedName: distinguishedName);
                    var directoryAttributeModification = new DirectoryAttributeModification
                    {
                        Name = GpLinkAttribute,
                        Operation = DirectoryAttributeOperation.Replace
                    };

                    var groupPolicyObjectDistinguishedName = $"CN={guid},CN=Policies,CN=System,{rootNamingContext}";
                    directoryAttributeModification.Add(gPLink.Replace($"[LDAP://{groupPolicyObjectDistinguishedName};2]", string.Empty));
                    modifyRequest.Modifications.Add(directoryAttributeModification);
                    var response = SendRequest(Logger, modifyRequest, connection);
                    if (response.ResultCode != ResultCode.Success)
                    {
                        throw new LdapException((int)response.ResultCode, response.ErrorMessage);
                    }
                }
            }
        });
    }
    
    private static string StandardizeGuid(string guid)
    {
        if (!guid.StartsWith('{'))
        {
            return "{" + guid + "}";
        }

        return guid;
    }

    private static byte[] CreateRegistryPol()
    {
        var editor = new RegistryPolFileEditor();
        return editor.Export();
    }

    private static IEnumerable<string> EnumerateLibraries()
    {
        var workingDirectory = Directory.GetParent(Environment.GetCommandLineArgs().First());
        if (workingDirectory is null) yield break;
        var filter = LibraryFilter();
        foreach (var file in Directory.GetFiles(workingDirectory.FullName, searchPattern: "*.dll", SearchOption.TopDirectoryOnly))
        {
            if (!filter.Contains(Path.GetFileName(file))) continue;
            yield return file;
        }
    }

    private static ISet<string> LibraryFilter()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "api-ms-win-crt-conio-l1-1-0.dll",
            "api-ms-win-crt-convert-l1-1-0.dll",
            "api-ms-win-crt-environment-l1-1-0.dll",
            "api-ms-win-crt-filesystem-l1-1-0.dll",
            "api-ms-win-crt-heap-l1-1-0.dll",
            "api-ms-win-crt-locale-l1-1-0.dll",
            "api-ms-win-crt-math-l1-1-0.dll",
            "api-ms-win-crt-multibyte-l1-1-0.dll",
            "api-ms-win-crt-private-l1-1-0.dll",
            "api-ms-win-crt-process-l1-1-0.dll",
            "api-ms-win-crt-runtime-l1-1-0.dll",
            "api-ms-win-crt-stdio-l1-1-0.dll",
            "api-ms-win-crt-string-l1-1-0.dll",
            "api-ms-win-crt-time-l1-1-0.dll",
            "api-ms-win-crt-utility-l1-1-0.dll",
            "concrt140.dll",
            "mfc140.dll",
            "mfc140chs.dll",
            "mfc140cht.dll",
            "mfc140deu.dll",
            "mfc140enu.dll",
            "mfc140esn.dll",
            "mfc140fra.dll",
            "mfc140ita.dll",
            "mfc140jpn.dll",
            "mfc140kor.dll",
            "mfc140rus.dll",
            "mfc140u.dll",
            "mfcm140.dll",
            "mfcm140u.dll",
            "msvcp140.dll",
            "msvcp140_1.dll",
            "msvcp140_2.dll",
            "msvcp140_atomic_wait.dll",
            "msvcp140_codecvt_ids.dll",
            "ucrtbase.dll",
            "vcamp140.dll",
            "vccorlib140.dll",
            "vcomp140.dll",
            "vcruntime140.dll",
            "vcruntime140_1.dll"
        };
    }
}