using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Collector.Services.Implementation.Agent.Privileges.Native;

internal sealed class AllocatedMemory : IDisposable
{
    [SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources",        Justification = "Not pointing to a native resource.")]
    private IntPtr _pointer;

    internal AllocatedMemory(int bytesRequired)
    {
        this._pointer = Marshal.AllocHGlobal(bytesRequired);
    }

    ~AllocatedMemory()
    {
        this.InternalDispose();
    }

    internal IntPtr Pointer
    {
        get
        {
            return this._pointer;
        }
    }

    public void Dispose()
    {
        this.InternalDispose();
        GC.SuppressFinalize(this);
    }

    private void InternalDispose()
    {
        if (this._pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(this._pointer);
            this._pointer = IntPtr.Zero;
        }
    }
}