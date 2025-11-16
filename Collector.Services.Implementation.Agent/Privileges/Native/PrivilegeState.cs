namespace Collector.Services.Implementation.Agent.Privileges.Native;

/// <summary>State of a <see cref="Privilege"/>, derived from <see cref="PrivilegeAttributes"/>.</summary>
internal enum PrivilegeState
{
    /// <summary>
    /// Privilege is disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// Privilege is enabled.
    /// </summary>
    Enabled,

    /// <summary>
    /// Privilege is removed.
    /// </summary>
    Removed
}