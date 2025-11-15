using System.Diagnostics.CodeAnalysis;

namespace Collector.Services.Implementation.Agent.Privileges.Native;

/// <summary>
///     <para>Privilege attributes that augment a <see cref="Privilege"/> with state information.</para>
/// </summary>
/// <remarks>
///     <para>Use the following checks to interpret privilege attributes:</para>
///     <para>
///         <c>// Privilege is disabled.<br/>if (attributes == PrivilegeAttributes.Disabled) { /* ... */ }</c>
///     </para>
///     <para>
///         <c>// Privilege is enabled.<br/>if ((attributes &amp; PrivilegeAttributes.Enabled) == PrivilegeAttributes.Enabled) { /* ... */ }</c>
///     </para>
///     <para>
///         <c>// Privilege is removed.<br/>if ((attributes &amp; PrivilegeAttributes.Removed) == PrivilegeAttributes.Removed) { /* ... */ }</c>
///     </para>
///     <para>To avoid having to work with a flags based enumerated type, use <see cref="ProcessExtensions.GetPrivilegeState(PrivilegeAttributes)"/> on attributes.</para>
/// </remarks>
[Flags, SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification = "Native enum."), SuppressMessage("Microsoft.Usage", "CA2217:DoNotMarkEnumsWithFlags", Justification = "Native enum.")]
internal enum PrivilegeAttributes
{
    /// <summary>Privilege is disabled.</summary>
    Disabled = 0,

    /// <summary>Privilege is enabled by default.</summary>
    EnabledByDefault = 1,

    /// <summary>Privilege is enabled.</summary>
    Enabled = 2,

    /// <summary>Privilege is removed.</summary>
    Removed = 4,

    /// <summary>Privilege used to gain access to an object or service.</summary>
    UsedForAccess = -2147483648
}