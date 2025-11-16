using System.Runtime.InteropServices;

namespace Collector.Services.Implementation.Agent.Privileges.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct LuidAndAttributes
{
    internal Luid Luid;

    internal PrivilegeAttributes Attributes;
}