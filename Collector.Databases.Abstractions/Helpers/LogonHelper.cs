namespace Collector.Databases.Abstractions.Helpers;

public static class LogonHelper
{
    public static long FromLogonId(string logonId)
    {
        return Convert.ToInt64(logonId, fromBase: 16);
    }
    
    public static string ToLogonId(long logonId)
    {
        return logonId.ToString("x");
    }
}