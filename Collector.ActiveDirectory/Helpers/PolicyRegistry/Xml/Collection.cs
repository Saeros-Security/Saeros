using YAXLib.Attributes;
using YAXLib.Enums;

namespace Collector.ActiveDirectory.Helpers.PolicyRegistry.Xml;

public class Collection
{
    [YAXDontSerializeIfNull]
    [YAXCollection(YAXCollectionSerializationTypes.Recursive, SerializationType = YAXCollectionSerializationTypes.RecursiveWithNoContainingElement)]
    public List<Collection>? Collections { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXCollection(YAXCollectionSerializationTypes.Recursive, SerializationType = YAXCollectionSerializationTypes.RecursiveWithNoContainingElement)]
    public List<Registry>? Registries { get; set; }
    
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
    [YAXSerializeAs("changed")]
    public string? Changed { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("uid")]
    public string? Uid { get; set; }
}