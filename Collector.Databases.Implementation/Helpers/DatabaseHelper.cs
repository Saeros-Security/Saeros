namespace Collector.Databases.Implementation.Helpers;

internal static class DatabaseHelper
{
    public static object GetValue(object? value)
    {
        if (value is null) return DBNull.Value;
        if (value is string s && string.IsNullOrWhiteSpace(s)) return DBNull.Value;
        if (value is bool b)
        {
            return b ? 1 : 0;
        }
        
        return value;
    }
}