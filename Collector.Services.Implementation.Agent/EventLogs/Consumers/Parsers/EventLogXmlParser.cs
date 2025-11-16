using Collector.Detection.Rules.Builders;
using TurboXml;

namespace Collector.Services.Implementation.Agent.EventLogs.Consumers.Parsers;

internal struct EventLogXmlParser : IXmlReadHandler
{
    public readonly IDictionary<string, string> Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private string? _propertyName;
    private string? _propertyValue;
    private string? _currentTag;

    private const string Name = "Name";
    private const string Data = "Data";
    
    public EventLogXmlParser()
    {
    }

    public void OnBeginTag(ReadOnlySpan<char> name, int line, int column)
    {
        _currentTag = new string(name);
    }

    public void OnEndTag(ReadOnlySpan<char> name, int line, int column)
    {
        if (_currentTag == Data && !string.IsNullOrEmpty(_propertyName) && !string.IsNullOrEmpty(_propertyValue))
        {
            if (!Properties.TryAdd(_propertyName, _propertyValue))
            {
                if (Properties.TryGetValue(_propertyName, out var current))
                {
                    if (current.Contains(Constants.AbnormalSeparator))
                    {
                        var entries = current.Split(Constants.AbnormalSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
                        entries.Add(_propertyValue);
                        Properties[_propertyName] = string.Join(Constants.AbnormalSeparator, entries);
                    }
                    else
                    {
                        Properties[_propertyName] = string.Join(Constants.AbnormalSeparator, new List<string> { current, _propertyValue });
                    }
                }
            }
        }
    }

    public void OnAttribute(ReadOnlySpan<char> name, ReadOnlySpan<char> value, int nameLine, int nameColumn, int valueLine, int valueColumn)
    {
        _propertyName = Name.Equals(new string(name), StringComparison.OrdinalIgnoreCase) ? new string(value) : null;
    }
    
    public void OnText(ReadOnlySpan<char> text, int line, int column)
    {
        _propertyValue = new string(text);
    }
}