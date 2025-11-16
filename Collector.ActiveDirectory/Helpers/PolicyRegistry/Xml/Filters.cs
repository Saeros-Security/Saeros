using YAXLib.Attributes;
using YAXLib.Enums;

namespace Collector.ActiveDirectory.Helpers.PolicyRegistry.Xml;

public class Filters
{
    [YAXDontSerializeIfNull]
    [YAXCollection(YAXCollectionSerializationTypes.Recursive, SerializationType = YAXCollectionSerializationTypes.RecursiveWithNoContainingElement)]
    [YAXSerializeAs("FilterFile")]
    public List<FilterFile>? FilterFiles { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("clsid")]
    public string? Clsid { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("name")]
    public string? Name { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("status")]
    public string? Status { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("descr")]
    public string? Description { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("image")]
    public string? Image { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("changed")]
    public string? Changed { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("uid")]
    public string? Uid { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("bypassErrors")]
    public string? BypassErrors { get; set; }
}