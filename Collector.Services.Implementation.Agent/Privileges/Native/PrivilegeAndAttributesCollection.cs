using System.Collections.ObjectModel;
using System.Text;

namespace Collector.Services.Implementation.Agent.Privileges.Native;

/// <summary>Read-only collection of privilege and attributes.</summary>
[Serializable]
internal sealed class PrivilegeAndAttributesCollection : ReadOnlyCollection<PrivilegeAndAttributes>
{
    internal PrivilegeAndAttributesCollection(IList<PrivilegeAndAttributes> list)
        : base(list)
    {
    }

    /// <summary>Returns a <see cref="string"/> representation of the collection.</summary>
    /// <returns><see cref="string"/> representation of the collection.</returns>
    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        int maxPrivilegeLength = this.Max(privilegeAndAttributes => privilegeAndAttributes.Privilege.ToString().Length);
        foreach (PrivilegeAndAttributes privilegeAndAttributes in this)
        {
            stringBuilder.Append(privilegeAndAttributes.Privilege);
            int paddingLength = maxPrivilegeLength - privilegeAndAttributes.Privilege.ToString().Length;
            char[] padding = new char[paddingLength];
            for (int i = 0; i < paddingLength; i++)
            {
                padding[i] = ' ';
            }

            stringBuilder.Append(padding);
            stringBuilder.Append(" => ");
            stringBuilder.AppendLine(privilegeAndAttributes.PrivilegeAttributes.ToString());
        }

        return stringBuilder.ToString();
    }
}