namespace Collector.Databases.Implementation.Stores.Tracing.Helpers;

public enum LogonType
{
    InteractiveLogon = 2,
    NetworkLogon = 3,
    BatchLogon = 4,
    ServiceLogon = 5,
    UnlockLogon = 7,
    NetworkClearTextLogon = 8,
    NewCredentialsLogon = 9,
    RemoteInteractiveLogon = 10,
    CachedInteractiveLogon = 11
}

internal static class LogonTypes
{
    public static readonly Dictionary<string, LogonType> Types = new()
    {
        { "Interactive Logon", LogonType.InteractiveLogon },
        { "Network Logon", LogonType.NetworkLogon },
        { "Batch Logon", LogonType.BatchLogon },
        { "Service Logon", LogonType.ServiceLogon },
        { "Unlock Logon", LogonType.UnlockLogon },
        { "Network Clear Text Logon", LogonType.NetworkClearTextLogon },
        { "New Credentials Logon", LogonType.NewCredentialsLogon },
        { "Remote Interactive Logon", LogonType.RemoteInteractiveLogon },
        { "Cached Interactive Logon", LogonType.CachedInteractiveLogon }
    };
    
    public static readonly Dictionary<LogonType, string> ReversedTypes = new()
    {
        { LogonType.InteractiveLogon, "Interactive Logon" },
        { LogonType.NetworkLogon, "Network Logon" },
        { LogonType.BatchLogon, "Batch Logon" },
        { LogonType.ServiceLogon, "Service Logon" },
        { LogonType.UnlockLogon, "Unlock Logon" },
        { LogonType.NetworkClearTextLogon, "Network Clear Text Logon" },
        { LogonType.NewCredentialsLogon, "New Credentials Logon" },
        { LogonType.RemoteInteractiveLogon, "Remote Interactive Logon" },
        { LogonType.CachedInteractiveLogon, "Cached Interactive Logon" }
    };
}