namespace Collector.Services.Implementation.Agent.Privileges.Native;

/// <summary>Structure that links <see cref="Privilege"/> and <see cref="PrivilegeAttributes"/> together.</summary>
internal struct PrivilegeAndAttributes : IEquatable<PrivilegeAndAttributes>
{
    private readonly Privilege _privilege;
    private readonly PrivilegeAttributes _privilegeAttributes;

    internal PrivilegeAndAttributes(Privilege privilege, PrivilegeAttributes privilegeAttributes)
    {
        this._privilege = privilege;
        this._privilegeAttributes = privilegeAttributes;
    }

    /// <summary>Gets the privilege.</summary>
    /// <value>The privilege.</value>
    public Privilege Privilege
    {
        get
        {
            return this._privilege;
        }
    }

    /// <summary>Gets the privilege attributes.</summary>
    /// <value>The privilege attributes.</value>
    public PrivilegeAttributes PrivilegeAttributes
    {
        get
        {
            return this._privilegeAttributes;
        }
    }

    /// <summary>Gets the privilege state.</summary>
    /// <value>The privilege state.</value>
    /// <remarks>Derived from <see cref="PrivilegeAttributes"/>.</remarks>
    public PrivilegeState PrivilegeState
    {
        get
        {
            return ProcessExtensions.GetPrivilegeState(this._privilegeAttributes);
        }
    }

    /// <summary>Compares two instances for equality.</summary>
    /// <param name="first">First instance.</param>
    /// <param name="second">Second instance.</param>
    /// <returns>Value indicating equality of instances.</returns>
    public static bool operator ==(PrivilegeAndAttributes first, PrivilegeAndAttributes second)
    {
        return first.Equals(second);
    }

    /// <summary>Compares two instances for inequality.</summary>
    /// <param name="first">First instance.</param>
    /// <param name="second">Second instance.</param>
    /// <returns>Value indicating inequality of instances.</returns>
    public static bool operator !=(PrivilegeAndAttributes first, PrivilegeAndAttributes second)
    {
        return !first.Equals(second);
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>The hash code for this instance.</returns>
    public override int GetHashCode()
    {
        return this._privilege.GetHashCode() ^ this._privilegeAttributes.GetHashCode();
    }

    /// <summary>Indicates whether this instance and a specified object are equal.</summary>
    /// <param name="obj">Another object to compare to.</param>
    /// <returns>Value indicating whether this instance and a specified object are equal.</returns>
    public override bool Equals(object? obj)
    {
        return obj is PrivilegeAndAttributes ? this.Equals((PrivilegeAndAttributes)obj) : false;
    }

    /// <summary>Indicates whether this instance and another instance are equal.</summary>
    /// <param name="other">Another instance to compare to.</param>
    /// <returns>Value indicating whether this instance and another instance are equal.</returns>
    public bool Equals(PrivilegeAndAttributes other)
    {
        return this._privilege == other.Privilege && this._privilegeAttributes == other.PrivilegeAttributes;
    }
}