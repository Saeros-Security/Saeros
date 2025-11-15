using System.Runtime.InteropServices;

namespace Collector.Services.Implementation.Agent.Privileges.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct TokenPrivilege
{
    internal int PrivilegeCount;

    internal LuidAndAttributes Privilege;
}