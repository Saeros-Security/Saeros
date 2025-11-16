namespace Collector.Detection.Rules.Aggregations;

internal record Aggregation(string? Condition, string? Property, string[] Dimensions, string Operator, string Value);
