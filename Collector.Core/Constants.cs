namespace Collector.Core;

public static class Constants
{
    public static class Application
    {
        public static readonly string NamedPipeName = $"{Shared.Constants.CompanyName}_Collector";
    }
    
    public const int MaxRetentionDays = 365;
}