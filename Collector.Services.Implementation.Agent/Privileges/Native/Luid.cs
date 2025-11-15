using System.Runtime.InteropServices;

namespace Collector.Services.Implementation.Agent.Privileges.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    internal int LowPart;

    internal int HighPart;
}