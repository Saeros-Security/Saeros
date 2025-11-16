using Collector.Logging;
using Serilog;
using Serilog.Configuration;
using Shared.Extensions;

namespace Collector.Extensions;

internal static class LoggerEnrichmentConfigurationExtensions
{
    public static LoggerConfiguration WithAssemblyVersion(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        var version = typeof(LoggerEnrichmentConfigurationExtensions).Assembly.GetVersion();
        return enrichmentConfiguration.WithProperty(nameof(Version), version);
    }
    
    public static LoggerConfiguration WithSourceContext(this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        return enrichmentConfiguration.With<SourceContextEnricher>();
    }
}