namespace Collector.Services.Abstractions.EventProviders.Registries;

public static class EventProviderRegistry
{
    public const string UserSession = "User";
    public const string KernelSession = "Kernel";
    public static readonly string UserTrace = $"{Shared.Constants.CompanyName}-{Shared.Constants.CollectorName}-{UserSession}";
    public static readonly string KernelTrace = $"{Shared.Constants.CompanyName}-{Shared.Constants.CollectorName}-{KernelSession}";
}