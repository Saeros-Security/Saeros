using Collector.Detection.Aggregations.Aggregators;
using Collector.Detection.Rules.Aggregations;
using Collector.Detection.Rules.Correlations;
using Detection.Yaml;
using Serilog;
using Shared;

namespace Collector.Detection.Rules.Extensions;

internal static class AggregationExtensions
{
    private static Func<string, bool> ContainsColumn(string ruleId)
    {
        return column => ContainsColumn(ruleId, column);
    }
    
    private static bool ContainsColumn(string ruleId, string column)
    {
        return Aggregator.Instance.ContainsColumn(ruleId, column);
    }
    
    public static WinEvent? Aggregate(this Aggregation aggregation, YamlRule rule)
    {
        try
        {
            foreach (var winEvent in Aggregator.Instance.Query(rule.Id, BuildQuery(aggregation, ContainsColumn(rule.Id))))
            {
                return Aggregator.Instance.Matched(rule.Id, match: winEvent);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error has occurred while computing aggregation for rule {Rule}", rule.Id);
        }
        
        return null;
    }

    private static string BuildQuery(Aggregation aggregation, Func<string, bool> columnFilter)
    {
        if (aggregation.Property == null && aggregation.Dimensions.Length == 0)
        {
            return BuildQuery(aggregation.Operator.ToOperator(), aggregation.Value, columnFilter, dimensions: null);
        }

        if (aggregation.Property == null && aggregation.Dimensions.Length > 0)
        {
            return BuildQuery(aggregation.Operator.ToOperator(), aggregation.Value, columnFilter, aggregation.Dimensions);
        }

        if (aggregation.Property is not null && aggregation.Dimensions.Length == 0)
        {
            return BuildQuery(aggregation.Operator.ToOperator(), aggregation.Value, aggregation.Property, columnFilter, dimensions: null);
        }

        if (aggregation.Property is not null && aggregation.Dimensions.Length > 0)
        {
            return BuildQuery(aggregation.Operator.ToOperator(), aggregation.Value, aggregation.Property, columnFilter, aggregation.Dimensions);
        }

        throw new ArgumentException("Invalid aggregation");
    }

    private static string BuildQuery(Operator @operator, string value, Func<string, bool> columnFilter, string[]? dimensions)
    {
        if (!int.TryParse(value, out var operand)) throw new Exception($"Could not parse aggregation operand: {value}");
        if (@operator == Operator.Equal)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(*) AS Count
                            FROM Aggregations
                        HAVING 
                            Count = {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(*) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count = {operand}
                    """;
        }

        if (@operator == Operator.GreaterThanOrEqual)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(*) AS Count
                            FROM Aggregations
                        HAVING 
                            Count >= {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(*) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count >= {operand}
                    """;
        }
        
        if (@operator == Operator.GreaterThan)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(*) AS Count
                            FROM Aggregations
                        HAVING 
                            Count > {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(*) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count > {operand}
                    """;
        }
        
        if (@operator == Operator.LessThanOrEqual)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(*) AS Count
                            FROM Aggregations
                        HAVING 
                            Count <= {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(*) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count <= {operand}
                    """;
        }
        
        if (@operator == Operator.LessThan)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(*) AS Count
                            FROM Aggregations
                        HAVING 
                            Count < {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(*) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count < {operand}
                    """;
        }

        throw new ArgumentException("Invalid aggregation");
    }

    private static string BuildQuery(Operator @operator, string value, string property, Func<string, bool> columnFilter, string[]? dimensions)
    {
        if (!int.TryParse(value, out var operand)) throw new Exception($"Could not parse aggregation operand: {value}");
        if (!columnFilter(property)) return "SELECT 0 WHERE 0;";
        if (@operator == Operator.Equal)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(DISTINCT {property}) AS Count
                            FROM Aggregations
                        HAVING 
                            Count = {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(DISTINCT {property}) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count = {operand}
                    """;
        }

        if (@operator == Operator.GreaterThanOrEqual)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(DISTINCT {property}) AS Count
                            FROM Aggregations
                        HAVING 
                            Count >= {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(DISTINCT {property}) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count >= {operand}
                    """;
        }

        if (@operator == Operator.GreaterThan)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(DISTINCT {property}) AS Count
                            FROM Aggregations
                        HAVING 
                            Count > {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(DISTINCT {property}) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count > {operand}
                    """;
        }

        if (@operator == Operator.LessThanOrEqual)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(DISTINCT {property}) AS Count
                            FROM Aggregations
                        HAVING 
                            Count <= {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(DISTINCT {property}) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count <= {operand}
                    """;
        }

        if (@operator == Operator.LessThan)
        {
            if (dimensions == null)
            {
                return $"""
                        SELECT *, COUNT(DISTINCT {property}) AS Count
                            FROM Aggregations
                        HAVING 
                            Count < {operand}
                        """;
            }

            if (!dimensions.Where(columnFilter).Any()) return "SELECT 0 WHERE 0;";
            return $"""
                    SELECT *, COUNT(DISTINCT {property}) AS Count
                        FROM Aggregations
                    GROUP BY {string.Join(",", dimensions.Where(columnFilter))}
                    HAVING 
                        Count < {operand}
                    """;
        }

        throw new ArgumentException("Invalid aggregation");
    }
}