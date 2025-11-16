using YAXLib.Attributes;

namespace Collector.ActiveDirectory.Helpers.PolicyRegistry.Xml;

public class Properties
{
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("action")]
    public string? Action { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("displayDecimal")]
    public string? DisplayDecimal { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("default")]
    public string? Default { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("hive")]
    public string? Hive { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("key")]
    public string? Key { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("name")]
    public string? Name { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("type")]
    public string? Type { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("value")]
    public string? Value { get; set; }
}