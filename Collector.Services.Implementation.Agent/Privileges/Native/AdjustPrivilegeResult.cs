namespace Collector.Services.Implementation.Agent.Privileges.Native;

/// <summary>Result from a privilege adjustment.</summary>
internal enum AdjustPrivilegeResult
{
    /// <summary>Privilege not modified.</summary>
    None,

    /// <summary>Privilege modified.</summary>
    PrivilegeModified
}