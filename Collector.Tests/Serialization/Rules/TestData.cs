using Shared;

namespace Collector.Tests.Serialization.Rules;

public class TestData(string yamlRule) : ITestData
{
    protected void Add(WinEvent winEvent) => WinEvents.Add(winEvent);
    
    public string YamlRule { get; } = yamlRule;
    public IList<WinEvent> WinEvents { get; } = new List<WinEvent>();
    public bool Match { get; protected init; }
    public string? Details { get; protected init; }
}