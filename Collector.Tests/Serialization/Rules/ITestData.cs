using Shared;

namespace Collector.Tests.Serialization.Rules;

public interface ITestData
{
    string YamlRule { get; }
    IList<WinEvent> WinEvents { get; }
    bool Match { get; }
    string? Details { get; }
}