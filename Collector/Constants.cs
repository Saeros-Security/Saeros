namespace Collector;

internal static class Constants
{
    public static class Application
    {
        public static readonly string EnvironmentVariableNamePrefix = $"{Shared.Constants.CompanyName}_";
    }
    
    public static class Logging
    {
        public const string LogTemplate = "[{Timestamp:u} {Level:u}] {Message:lj} {Properties}{NewLine}{Exception}";
    }
}