using App.Metrics.Counter;
using App.Metrics.Gauge;

namespace Collector.Services.Implementation.Bridge.Dashboards.Extensions;

internal static class EnumerableExtensions
{    
    public static IEnumerable<CounterValueSource> GetFromToday(this CounterValueSource[] source)
    {
        var today = DateTime.Today.ToString("O");
        return source.Where(r => r.Tags.Values.Contains(today));
    }
    
    public static IEnumerable<GaugeValueSource> GetFromToday(this GaugeValueSource[] source)
    {
        var today = DateTime.Today.ToString("O");
        return source.Where(r => r.Tags.Values.Contains(today));
    }
}