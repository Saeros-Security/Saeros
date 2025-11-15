using YAXLib.Attributes;
using YAXLib.Enums;

namespace Collector.Tests.Hayabusa.EventRecords;

[YAXSerializableType(Options = YAXSerializationOptions.DontSerializeNullObjects)]
[YAXNamespace("http://schemas.microsoft.com/win/2004/08/events/event")]
public class YaxEventRecord
{
    [YAXSerializableField]
    public SystemData? System { get; set; }
    
    [YAXSerializableField]
    public EventData? EventData { get; set; }
}

public class SystemData
{
    public Provider? Provider { get; set; }
    public int EventID { get; set; }
    public string? Channel { get; set; }
    public string? Computer { get; set; }
}

public class Provider
{
    [YAXAttributeForClass]
    public string? Name { get; set; }
}

public class EventData
{
    [YAXElementFor("Data")]
    [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement)]
    public List<Data>? Items { get; set; }
}

public class Data
{
    [YAXAttributeForClass]
    public string? Name { get; set; }
        
    [YAXValueForClass]
    public string? Value { get; set; }
}