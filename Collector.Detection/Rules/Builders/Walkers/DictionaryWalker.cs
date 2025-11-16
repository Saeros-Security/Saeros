using System.Collections;
using System.Linq.Expressions;
using Collector.Detection.Rules.Expressions.Predicates;
using Shared;

namespace Collector.Detection.Rules.Builders.Walkers;

internal static class DictionaryWalker
{
    public static Expression<Func<WinEvent, bool>> Walk(Action<Expression<Func<WinEvent, bool>>> onExpressionBuilt, IDictionary<string, object> properties, bool nested, ISet<string> domainControllers, Func<string, bool> canProcessRegex, Action<string> onRegexFailure, string? parentNodeName = null)
    {
        var expression = PredicateBuilder.New<WinEvent>(defaultExpression: true);
        foreach (var property in properties)
        {
            if (property.Value is string value)
            {
                var currentExpression = ExpressionBuilder.BuildMatchExpression(property.Key, value, parentNodeName, domainControllers, canProcessRegex, onRegexFailure);
                if (!nested)
                {
                    onExpressionBuilt(currentExpression);
                }
                
                expression = expression.And(currentExpression);
            }
            else if (property.Value is IEnumerable<object> enumerable)
            {
                var currentExpression = EnumerableWalker.Walk(onExpressionBuilt, properties: enumerable.Select(prop => new KeyValuePair<string, object>(property.Key, prop)).Cast<object>(), nested: true, domainControllers, canProcessRegex, onRegexFailure, parentNodeName: property.Key, shouldBeAnd: property.Key.Contains(Constants.All));
                if (!nested)
                {
                    onExpressionBuilt(currentExpression);
                }
                
                expression = expression.And(currentExpression);
            }
            else if (property.Value is IDictionary dictionary)
            {
                switch (dictionary)
                {
                    case IDictionary<string, object> objectValue:
                        var currentExpression = Walk(onExpressionBuilt, objectValue, nested: true, domainControllers, canProcessRegex, onRegexFailure, parentNodeName: property.Key);
                        if (!nested)
                        {
                            onExpressionBuilt(currentExpression);
                        }
                        
                        expression = expression.And(currentExpression);
                        break;
                    case IDictionary<string, string> stringValue:
                        currentExpression = Walk(onExpressionBuilt, properties: stringValue.ToDictionary(kvp => kvp.Key, object (kvp) => kvp.Value, StringComparer.Ordinal), nested: true, domainControllers, canProcessRegex, onRegexFailure, parentNodeName: property.Key);
                        if (!nested)
                        {
                            onExpressionBuilt(currentExpression);
                        }
                        
                        expression = expression.And(currentExpression);
                        break;
                    default:
                        throw new Exception($"Value of type {dictionary.GetType()} is not supported");
                }
            }
            else
            {
                throw new Exception($"Property of type {property.Value.GetType()} is not supported");
            }
        }

        return expression;
    }
}