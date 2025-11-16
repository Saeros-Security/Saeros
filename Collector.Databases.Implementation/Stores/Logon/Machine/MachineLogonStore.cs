using System.DirectoryServices.AccountManagement;
using Microsoft.Extensions.Logging;
using Shared.Helpers;

namespace Collector.Databases.Implementation.Stores.Logon.Machine;

public sealed class MachineLogonStore(ILogger<MachineLogonStore> logger) : LogonStore(logger, ContextType.Machine)
{
    protected override Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        AddComputer(new Computer(MachineNameHelper.FullyQualifiedName, System.Runtime.InteropServices.RuntimeInformation.OSDescription));
        return Task.CompletedTask;
    }
}