namespace Collector.Detection.Rules.Correlations.Extensions;

internal static class CorrelationExtensions
{
    public static CorrelationType ToCorrelationType(this string correlation)
    {
        return correlation switch
        {
            "event_count" => CorrelationType.EventCount,
            "value_count" => CorrelationType.ValueCount,
            _ => throw new ArgumentException($"Unknown correlation: {correlation}")
        };
    }
}