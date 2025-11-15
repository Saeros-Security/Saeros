namespace Collector.Detection.Rules.Detections;

internal sealed class Detection(string name, object properties)
{
    public string Name { get; } = name;
    public object Properties { get; } = properties;

    public static Detection Create(KeyValuePair<string, object> kvp)
    {
        return new Detection(kvp.Key, kvp.Value);
    }
}