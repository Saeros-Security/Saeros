using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Collector.Tests.Hayabusa.Converters;

public sealed class DynamicObjectConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(object) || typeof(IEnumerable<object>).IsAssignableFrom(type);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<MappingStart>(out _))
        {
            return ParseMapping(parser);
        }

        if (parser.TryConsume<SequenceStart>(out _))
        {
            return ParseSequence(parser);
        }

        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return scalar.Value;
        }

        throw new InvalidOperationException("Expected a YAML object or array");
    }

    private static object ParseMapping(IParser parser)
    {
        var complex = false;
        var dictionary = new Dictionary<string, object>();
        while (!parser.Accept<MappingEnd>(out _))
        {
            if (parser.Accept<Scalar>(out _))
            {
                var key = parser.Consume<Scalar>();
                if (parser.TryConsume<SequenceStart>(out _))
                {
                    dictionary[key.Value] = ParseSequence(parser);
                    complex = true;
                }

                if (parser.TryConsume<Scalar>(out var scalar))
                {
                    dictionary[key.Value] = scalar.Value;
                }

                if (parser.TryConsume<MappingStart>(out _))
                {
                    dictionary[key.Value] = ParseMapping(parser);
                    complex = true;
                }
            }
        }

        parser.MoveNext();
        if (complex)
        {
            return dictionary;
        }

        return dictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
    }

    private static object ParseSequence(IParser parser)
    {
        var complex = false;
        var enumerable = new List<object>();
        while (!parser.Accept<SequenceEnd>(out _))
        {
            if (parser.TryConsume<MappingStart>(out _))
            {
                enumerable.Add(ParseMapping(parser));
                complex = true;
            }

            if (parser.TryConsume<Scalar>(out var scalar))
            {
                enumerable.Add(scalar.Value);
            }
        }

        parser.MoveNext();
        if (complex)
        {
            return enumerable;
        }

        return enumerable.Cast<string>().ToList();
    }


    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        throw new NotImplementedException();
    }
}