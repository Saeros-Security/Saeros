using System.Text.Json.Serialization;

namespace Collector.Detection.Rules.Mappings;

[method: JsonConstructor]
public sealed class PropertyMapping(IDictionary<string, Dictionary<string, string>> propertyValueByNames, IEnumerable<string> propertiesFromHexToDecimal)
{
    public IDictionary<string, Dictionary<string, string>> PropertyValueByNames { get; } = propertyValueByNames;
    public IEnumerable<string> PropertiesFromHexToDecimal { get; } = propertiesFromHexToDecimal;
}