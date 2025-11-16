namespace Collector.Databases.Implementation.Extensions;

public static class DateTimeOffsetExtensions
{
    public static DateTimeOffset TrimToMinutes(this DateTimeOffset date)
    {
        return new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0, 0, 0, TimeSpan.Zero);
    }
}