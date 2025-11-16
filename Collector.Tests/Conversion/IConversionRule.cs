namespace Collector.Tests.Conversion;

public interface IConversionRule
{
    public string Yaml { get; }
    public string Conversion { get; }
}