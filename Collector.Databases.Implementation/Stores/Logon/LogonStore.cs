using System.Diagnostics.CodeAnalysis;
using System.DirectoryServices.AccountManagement;
using Collector.Databases.Abstractions.Helpers;
using Collector.Databases.Abstractions.Stores.Logon;
using Collector.Databases.Implementation.Caching.LRU;
using Collector.Databases.Implementation.Helpers;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Helpers;

namespace Collector.Databases.Implementation.Stores.Logon;

public abstract class LogonStore(ILogger logger, ContextType contextType) : ILogonStore
{
    protected sealed record Computer(string Name, string OperatingSystem);
    
    private readonly ISet<string> _privilegedUserSid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, string> _sidByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, string> _sidByDisplayName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, string> _sidBySamAccountName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, string> _sidByUserPrincipalName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, string> _operatingSystemByComputerName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentLogonDictionary _successLogons = new(capacity: 10, eviction: TimeSpan.FromMinutes(1));
    private readonly ConcurrentLogonDictionary _failureLogons = new(capacity: 10, eviction: TimeSpan.FromMinutes(1));
    
    protected static ValueTask<string> GetIpAddressAsync(string workstationName, CancellationToken cancellationToken)
    {
        return IpAddressResolver.GetIpAddressAsync(workstationName, cancellationToken);
    }
    
    private void LoadGroups()
    {
        using var context = new PrincipalContext(contextType);
        using var principalSearcher = new PrincipalSearcher();
        using var groupPrincipal = new GroupPrincipal(context);
        groupPrincipal.Name = "*";
        principalSearcher.QueryFilter = groupPrincipal;
        foreach (var group in principalSearcher.FindAll().OfType<GroupPrincipal>())
        {
            if (PrivilegeHelper.IsNativelyPrivileged(group.Sid.Value))
            {
                foreach (var member in group.GetMembers(recursive: true))
                {
                    if (member is UserPrincipal userPrincipal)
                    {
                        _privilegedUserSid.Add(userPrincipal.Sid.Value);
                    }
                }
            }
        }
    }
    
    private void LoadUsers()
    {
        using var context = new PrincipalContext(contextType);
        using var principalSearcher = new PrincipalSearcher();
        using var userPrincipal = new UserPrincipal(context);
        userPrincipal.Name = "*";
        principalSearcher.QueryFilter = userPrincipal;
        foreach (var user in principalSearcher.FindAll().OfType<UserPrincipal>())
        {
            if (PrivilegeHelper.IsNativelyPrivileged(user.Sid.Value))
            {
                _privilegedUserSid.Add(user.Sid.Value);
            }

            if (!string.IsNullOrWhiteSpace(user.Name))
            {
                _sidByName[user.Name] = user.Sid.Value;
            }

            if (!string.IsNullOrWhiteSpace(user.DisplayName))
            {
                _sidByDisplayName[user.DisplayName] = user.Sid.Value;
            }

            if (!string.IsNullOrWhiteSpace(user.SamAccountName))
            {
                _sidBySamAccountName[user.SamAccountName] = user.Sid.Value;
            }

            if (!string.IsNullOrWhiteSpace(user.UserPrincipalName))
            {
                _sidByUserPrincipalName[user.UserPrincipalName] = user.Sid.Value;
            }
        }
    }
    
    protected abstract Task LoadCoreAsync(CancellationToken cancellationToken);

    protected void AddComputer(Computer computer)
    {
        _operatingSystemByComputerName[computer.Name] = computer.OperatingSystem;
    }
    
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            _privilegedUserSid.Clear();
            _privilegedUserSid.Add(WellKnownSids.LocalSystem.Sid);
            _sidByName.Clear();
            _sidByDisplayName.Clear();
            _sidBySamAccountName.Clear();
            _sidByUserPrincipalName.Clear();
            await LoadCoreAsync(cancellationToken);
            LoadGroups();
            LoadUsers();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancellation has occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error has occurred");
        }
    }

    public void AddSuccessLogon(AccountLogon logon) => _successLogons.AddOrUpdate(logon, cumulative: false);
    
    public void AddFailureLogon(AccountLogon logon) => _failureLogons.AddOrUpdate(logon, cumulative: false);
    
    public IEnumerable<AccountLogon> EnumerateSuccessLogons() => _successLogons.Enumerate();

    public IEnumerable<AccountLogon> EnumerateFailureLogons() => _failureLogons.Enumerate();

    public bool TryGetWorkstationNameFromIpAddress(string ipAddress, [MaybeNullWhen(false)] out string workstationName)
    {
        return Lrus.WorkstationNameByIpAddress.TryGet(ipAddress, out workstationName);
    }
    
    public bool TryGetSidByUser(string username, [MaybeNullWhen(false)] out string sid)
    {
        return _sidByName.TryGetValue(username, out sid) || _sidByDisplayName.TryGetValue(username, out sid) || _sidBySamAccountName.TryGetValue(username, out sid) || _sidByUserPrincipalName.TryGetValue(username, out sid);
    }

    public bool TryGetOperatingSystemByComputer(string computer, [MaybeNullWhen(false)] out string operatingSystem)
    {
        return _operatingSystemByComputerName.TryGetValue(computer, out  operatingSystem);
    }

    public bool IsUserPrivileged(WinEvent winEvent)
    {
        if (winEvent.EventData.TryGetValue(nameof(Logon4624.SubjectLogonId), out var subjectLogonId) && LogonHelper.FromLogonId(subjectLogonId) > 0 &&
            winEvent.EventData.TryGetValue(nameof(Logon4624.SubjectUserName), out var subjectUsername) && !subjectUsername.EndsWith('$') &&
            winEvent.EventData.TryGetValue(nameof(Logon4624.SubjectUserSid), out var subjectUserSid) && !subjectUserSid.Equals(WellKnownSids.Nobody.Sid, StringComparison.Ordinal))
        {
            if (_privilegedUserSid.Contains(subjectUserSid))
            {
                return true;
            }
        }
        
        if (winEvent.EventData.TryGetValue(nameof(Logon4624.TargetLogonId), out var targetLogonId) && LogonHelper.FromLogonId(targetLogonId) > 0 &&
            winEvent.EventData.TryGetValue(nameof(Logon4624.TargetUserName), out var targetUsername) && !targetUsername.EndsWith('$') &&
            winEvent.EventData.TryGetValue(nameof(Logon4624.TargetUserSid), out var targetUserSid) && !targetUserSid.Equals(WellKnownSids.Nobody.Sid, StringComparison.Ordinal))
        {
            if (_privilegedUserSid.Contains(targetUserSid))
            {
                return true;
            }
        }

        if (winEvent.EventData.TryGetValue(nameof(Logon4624.SubjectUserName), out subjectUsername))
        {
            if (_sidByName.TryGetValue(subjectUsername, out var sid) && _privilegedUserSid.Contains(sid)) return true;
            if (_sidByDisplayName.TryGetValue(subjectUsername, out sid) && _privilegedUserSid.Contains(sid)) return true;
            if (_sidBySamAccountName.TryGetValue(subjectUsername, out sid) && _privilegedUserSid.Contains(sid)) return true;
            if (_sidByUserPrincipalName.TryGetValue(subjectUsername, out sid) && _privilegedUserSid.Contains(sid)) return true;
        }
        
        if (winEvent.EventData.TryGetValue(nameof(Logon4624.TargetUserName), out targetUsername))
        {
            if (_sidByName.TryGetValue(targetUsername, out var sid) && _privilegedUserSid.Contains(sid)) return true;
            if (_sidByDisplayName.TryGetValue(targetUsername, out sid) && _privilegedUserSid.Contains(sid)) return true;
            if (_sidBySamAccountName.TryGetValue(targetUsername, out sid) && _privilegedUserSid.Contains(sid)) return true;
            if (_sidByUserPrincipalName.TryGetValue(targetUsername, out sid) && _privilegedUserSid.Contains(sid)) return true;
        }

        return false;
    }
}