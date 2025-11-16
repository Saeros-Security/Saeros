using System.Reflection;

namespace Collector.Core.Helpers;

public static class VersionHelper
{
    public static string GetCollectorVersion(this Assembly? assembly)
    {
        if (assembly is null)
            return string.Empty;
        
        return assembly.GetName().Version?.ToString(2) ?? string.Empty;
    }
}