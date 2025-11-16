namespace Collector.Services.Implementation.Agent.Privileges.Native;

[Flags]
internal enum AccessTypeMasks
{
    Delete = 65536,

    ReadControl = 131072,

    WriteDAC = 262144,

    WriteOwner = 524288,

    Synchronize = 1048576,

    StandardRightsRequired = 983040,

    StandardRightsRead = ReadControl,

    StandardRightsWrite = ReadControl,

    StandardRightsExecute = ReadControl,

    StandardRightsAll = 2031616,

    SpecificRightsAll = 65535
}