using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Collector.Databases.Abstractions.Domain.Tracing.Tracers;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Kernel;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Logon;
using Collector.Databases.Implementation.Stores.Tracing.Tracers.Process;

namespace Collector.Databases.Implementation.Stores.Tracing.Tracers.Extensions;

[RequiresUnreferencedCode("Calls System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver.DefaultJsonTypeInfoResolver()")]
[RequiresDynamicCode("Calls System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver.DefaultJsonTypeInfoResolver()")]
public static class TracerJsonExtensions
{
    public static readonly JsonSerializerOptions Options;
    
    static TracerJsonExtensions()
    {
        Options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    AddDerivedTypes(typeof(Tracer), [typeof(Logon4624), typeof(Logon4625), typeof(Process4688), typeof(Process4689), typeof(NetworkTracer), typeof(SuccessLogonTracer), typeof(FailureLogonTracer)])
                }
            }
        };
    }
    
    private static Action<JsonTypeInfo> AddDerivedTypes(Type baseType, IEnumerable<Type> derivedTypes) => typeInfo =>
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
        if (baseType.IsAssignableFrom(typeInfo.Type))
        {
            var applicableDerivedTypes = derivedTypes.Where(t => typeInfo.Type.IsAssignableFrom(t));
            var first = true;
            foreach (var type in applicableDerivedTypes)
            {
                if (first)
                {
                    typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                    {
                        TypeDiscriminatorPropertyName = "$type",
                        IgnoreUnrecognizedTypeDiscriminators = true,
                        UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                    };
                }
                
                first = false;
                typeInfo.PolymorphismOptions!.DerivedTypes.Add(new JsonDerivedType(type, type.Name));
            }
        }
    };
}