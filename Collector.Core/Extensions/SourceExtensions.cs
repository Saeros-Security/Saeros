namespace Collector.Core.Extensions;

public static class SourceExtensions
{
    public static string FromSource(this RuleSource source)
    {
        return source switch
        {
            RuleSource.Sigma => "Sigma",
            _ => string.Empty
        };
    }
}