using YAXLib.Attributes;

namespace Collector.ActiveDirectory.Helpers.PolicyRegistry.Xml;

public class FilterFile
{
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("bool")]
    public string? Bool { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("not")]
    public string? Not { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("path")]
    public string? Path { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("folder")]
    public string? Folder { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("type")]
    public string? Type { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("min")]
    public string? Min { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("max")]
    public string? Max { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("gte")]
    public string? Gte { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("lte")]
    public string? Lte { get; set; }
}