using System.ComponentModel;
using System.Diagnostics;
using System.Security.Permissions;

namespace Collector.Services.Implementation.Agent.Privileges.Native;

/// <summary>Enables privileges on a process in a safe way, ensuring that they are returned to their original state when an operation that requires a privilege completes.</summary>
/// <remarks>
///     <para>When disabled, privileges are enabled until the instance of the PrivilegeEnabler class is disposed.</para>
///     <para>If the privilege specified is already enabled, it is not modified and will not be disabled when the instance of the PrivilegeEnabler class is disposed.</para>
///     <para>If desired, multiple privileges can be specified in the constructor.</para>
///     <para>If using multiple instances on the same process, do not dispose of them out-of-order. Making use of a using statement, the recommended method, enforces this.</para>
///     <para>For more information on privileges, see:</para>
///     <para><a href="http://msdn.microsoft.com/en-us/library/aa379306.aspx">Privileges</a></para>
///     <para><a href="http://msdn.microsoft.com/en-us/library/bb530716.aspx">Privilege Constants</a></para>
/// </remarks>
internal sealed class PrivilegeEnabler : IDisposable
{
    private static readonly Dictionary<Privilege, PrivilegeEnabler> SharedPrivileges = new Dictionary<Privilege, PrivilegeEnabler>();
    private static readonly Dictionary<Process, AccessTokenHandle> AccessTokenHandles = new Dictionary<Process, AccessTokenHandle>();
    private AccessTokenHandle? _accessTokenHandle;
    private bool _disposed;
    private bool _ownsHandle;
    private Process? _process;

    /// <summary>Initializes a new instance of the PrivilegeEnabler class.</summary>
    /// <param name="accessTokenHandle">The <see cref="AccessTokenHandle"/> for a <see cref="Process"/> on which privileges should be enabled.</param>
    /// <exception cref="InvalidOperationException">Thrown when another instance exists and has not been disposed.</exception>
    /// <permission cref="SecurityAction.LinkDemand">Requires the immediate caller to have FullTrust.</permission>
    public PrivilegeEnabler(AccessTokenHandle accessTokenHandle)
    {
        this._accessTokenHandle = accessTokenHandle;
    }

    /// <summary>Initializes a new instance of the PrivilegeEnabler class.</summary>
    /// <param name="process">The <see cref="Process"/> on which privileges should be enabled.</param>
    /// <exception cref="InvalidOperationException">Thrown when another instance exists and has not been disposed.</exception>
    /// <permission cref="SecurityAction.LinkDemand">Requires the immediate caller to have FullTrust.</permission>
    public PrivilegeEnabler(Process process)
    {
        lock (AccessTokenHandles)
        {
            if (AccessTokenHandles.ContainsKey(process))
            {
                this._accessTokenHandle = AccessTokenHandles[process];
            }
            else
            {
                this._accessTokenHandle =
                    process.GetAccessTokenHandle(TokenAccessRights.AdjustPrivileges | TokenAccessRights.Query);
                AccessTokenHandles.Add(process, this._accessTokenHandle);
                this._ownsHandle = true;
            }
        }

        this._process = process;
    }

    /// <summary>Initializes a new instance of the PrivilegeEnabler class with the specified privileges to be enabled.</summary>
    /// <param name="accessTokenHandle">The <see cref="AccessTokenHandle"/> for a <see cref="Process"/> on which privileges should be enabled.</param>
    /// <param name="privileges">The privileges to be enabled.</param>
    /// <exception cref="Win32Exception">Thrown when an underlying Win32 function call does not succeed.</exception>
    /// <permission cref="SecurityAction.LinkDemand">Requires the immediate caller to have FullTrust.</permission>
    public PrivilegeEnabler(AccessTokenHandle accessTokenHandle, params Privilege[] privileges)
        : this(accessTokenHandle)
    {
        foreach (Privilege privilege in privileges)
        {
            this.EnablePrivilege(privilege);
        }
    }

    /// <summary>Initializes a new instance of the PrivilegeEnabler class with the specified privileges to be enabled.</summary>
    /// <param name="process">The <see cref="Process"/> on which privileges should be enabled.</param>
    /// <param name="privileges">The privileges to be enabled.</param>
    /// <exception cref="Win32Exception">Thrown when an underlying Win32 function call does not succeed.</exception>
    /// <permission cref="SecurityAction.LinkDemand">Requires the immediate caller to have FullTrust.</permission>
    public PrivilegeEnabler(Process process, params Privilege[] privileges)
        : this(process)
    {
        foreach (Privilege privilege in privileges)
        {
            this.EnablePrivilege(privilege);
        }
    }

    /// <summary>Finalizes an instance of the PrivilegeEnabler class.</summary>
    ~PrivilegeEnabler()
    {
        this.InternalDispose();
    }

    /// <summary>Disposes of an instance of the PrivilegeEnabler class.</summary>
    /// <exception cref="Win32Exception">Thrown when an underlying Win32 function call does not succeed.</exception>
    /// <permission cref="SecurityAction.Demand">Requires the call stack to have FullTrust.</permission>
    public void Dispose()
    {
        this.InternalDispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Enables the specified <see cref="Privilege"/>.</summary>
    /// <param name="privilege">The <see cref="Privilege"/> to be enabled.</param>
    /// <returns>
    ///     <para>Result from the privilege adjustment.</para>
    ///     <para>If the <see cref="Privilege"/> is already enabled, <see cref="AdjustPrivilegeResult.None"/> is returned.</para>
    ///     <para>If the <see cref="Privilege"/> is owned by another instance of the PrivilegeEnabler class, <see cref="AdjustPrivilegeResult.None"/> is returned.</para>
    ///     <para>If a <see cref="Privilege"/> is removed from a process, it cannot be enabled.</para>
    /// </returns>
    /// <remarks>
    ///     <para>When disabled, privileges are enabled until the instance of the PrivilegeEnabler class is disposed.</para>
    ///     <para>If the privilege specified is already enabled, it is not modified and will not be disabled when the instance of the PrivilegeEnabler class is disposed.</para>
    /// </remarks>
    /// <exception cref="Win32Exception">Thrown when an underlying Win32 function call does not succeed.</exception>
    /// <permission cref="SecurityAction.LinkDemand">Requires the immediate caller to have FullTrust.</permission>
    public AdjustPrivilegeResult EnablePrivilege(Privilege privilege)
    {
        lock (SharedPrivileges)
        {
            if (!SharedPrivileges.ContainsKey(privilege) &&
                this._accessTokenHandle?.GetPrivilegeState(privilege) == PrivilegeState.Disabled &&
                this._accessTokenHandle.EnablePrivilege(privilege) == AdjustPrivilegeResult.PrivilegeModified)
            {
                SharedPrivileges.Add(privilege, this);
                return AdjustPrivilegeResult.PrivilegeModified;
            }

            return AdjustPrivilegeResult.None;
        }
    }

    private void InternalDispose()
    {
        if (!this._disposed)
        {
            lock (SharedPrivileges)
            {
                Privilege[] privileges = SharedPrivileges
                    .Where(keyValuePair => keyValuePair.Value == this)
                    .Select(keyValuePair => keyValuePair.Key)
                    .ToArray();

                foreach (Privilege privilege in privileges)
                {
                    this._accessTokenHandle?.DisablePrivilege(privilege);
                    SharedPrivileges.Remove(privilege);
                }

                if (this._ownsHandle)
                {
                    this._accessTokenHandle?.Dispose();
                    if (this._accessTokenHandle != null && this._process != null)
                    {
                        lock (this._accessTokenHandle)
                        {
                            AccessTokenHandles.Remove(this._process);
                        }
                    }
                }

                this._accessTokenHandle = null;
                this._ownsHandle = false;
                this._process = null;

                this._disposed = true;
            }
        }
    }
}