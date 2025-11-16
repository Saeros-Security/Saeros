using YAXLib.Attributes;
using YAXLib.Enums;

namespace Collector.ActiveDirectory.Helpers.PolicyRegistry.Xml;

public class RegistrySettings
{
    [YAXDontSerializeIfNull]
    [YAXAttributeForClass]
    [YAXSerializeAs("clsid")]
    public string? Clsid { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXCollection(YAXCollectionSerializationTypes.Recursive, SerializationType = YAXCollectionSerializationTypes.RecursiveWithNoContainingElement)]
    public List<Registry>? Registries { get; set; }
    
    [YAXDontSerializeIfNull]
    [YAXCollection(YAXCollectionSerializationTypes.Recursive, SerializationType = YAXCollectionSerializationTypes.RecursiveWithNoContainingElement)]
    public List<Collection>? Collections { get; set; }
}