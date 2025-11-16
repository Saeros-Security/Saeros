using QuikGraph;

namespace Collector.Databases.Abstractions.Domain.Tracing.Buckets;

public sealed record EdgeRecord<TKey, TSource, TTarget>(TKey Key, TSource Source, TTarget Target, TracingNode SourceNode, TracingNode TargetNode);