using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Collector.Services.Implementation.Agent.EventLogs.Helpers;

public static class AccessHelper
{
    public static bool HaveAccess(RawSecurityDescriptor rsd, SecurityIdentifier sid, Func<AccessFlags, bool> hasAccess)
    {
        var racl = rsd.DiscretionaryAcl;
        var dacl = new DiscretionaryAcl(false, false, racl);

        var daclBuffer = new byte[dacl.BinaryLength];
        dacl.GetBinaryForm(daclBuffer, 0);

        var sidBuffer = new byte[sid.BinaryLength];
        sid.GetBinaryForm(sidBuffer, 0);

        var t = new TRUSTEE();
        BuildTrusteeWithSid(ref t, sidBuffer);

        uint access = 0;
        GetEffectiveRightsFromAcl(daclBuffer, ref t, ref access);
        return hasAccess((AccessFlags)access);
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void BuildTrusteeWithSid(ref TRUSTEE pTrustee, byte[] sid);

    [DllImport("advapi32.dll")]
    private static extern uint GetEffectiveRightsFromAcl(byte[] pacl, ref TRUSTEE pTrustee, ref uint pAccessRights);

    private enum MULTIPLE_TRUSTEE_OPERATION
    {
        NO_MULTIPLE_TRUSTEE,
        TRUSTEE_IS_IMPERSONATE
    }

    private enum TRUSTEE_FORM
    {
        TRUSTEE_IS_SID,
        TRUSTEE_IS_NAME,
        TRUSTEE_BAD_FORM,
        TRUSTEE_IS_OBJECTS_AND_SID,
        TRUSTEE_IS_OBJECTS_AND_NAME
    }

    private enum TRUSTEE_TYPE
    {
        TRUSTEE_IS_UNKNOWN,
        TRUSTEE_IS_USER,
        TRUSTEE_IS_GROUP,
        TRUSTEE_IS_DOMAIN,
        TRUSTEE_IS_ALIAS,
        TRUSTEE_IS_WELL_KNOWN_GROUP,
        TRUSTEE_IS_DELETED,
        TRUSTEE_IS_INVALID,
        TRUSTEE_IS_COMPUTER
    }

    private struct TRUSTEE
    {
        public IntPtr pMultipleTrustee;
        public MULTIPLE_TRUSTEE_OPERATION MultipleTrusteeOperation;
        public TRUSTEE_FORM TrusteeForm;
        public TRUSTEE_TYPE TrusteeType;
        public IntPtr ptstrName;
    }

    [Flags]
    public enum AccessFlags : uint
    {
        Read = 1,
        Write = 2,
        Clear = 4
    }
}