namespace Collector.Tests.Conversion;

public class SigmaRuleTestData<T> : TheoryData<string, string> where T : IConversionRule
{
    public SigmaRuleTestData()
    {
        var data = (T?)Activator.CreateInstance(typeof(T), []);
        if (data is null) return;
        Add(data.Yaml, data.Conversion);
    }
}