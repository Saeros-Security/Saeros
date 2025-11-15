using System.Diagnostics.CodeAnalysis;
using Shared;

namespace Collector.Databases.Abstractions.Stores.Logon;

public interface ILogonStore
{
    Task LoadAsync(CancellationToken cancellationToken);
    bool IsUserPrivileged(WinEvent winEvent);
    bool TryGetWorkstationNameFromIpAddress(string ipAddress, [MaybeNullWhen(false)] out string workstationName);
    bool TryGetSidByUser(string username, [MaybeNullWhen(false)] out string sid);
    bool TryGetOperatingSystemByComputer(string computer, [MaybeNullWhen(false)] out string operatingSystem);
    void AddSuccessLogon(AccountLogon logon);
    void AddFailureLogon(AccountLogon logon);
    IEnumerable<AccountLogon> EnumerateSuccessLogons();
    IEnumerable<AccountLogon> EnumerateFailureLogons();
}