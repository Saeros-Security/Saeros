using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Collector.Services.Implementation.Agent.Privileges.Native;

internal sealed class ProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal ProcessHandle(IntPtr processHandle, bool ownsHandle)
        : base(ownsHandle)
    {
        handle = processHandle;
    }

    protected override bool ReleaseHandle()
    {
        if (!NativeMethods.CloseHandle(handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return true;
    }
}