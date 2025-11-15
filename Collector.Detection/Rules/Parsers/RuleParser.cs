using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Collector.Detection.Rules.Aggregations;
using Collector.Detection.Rules.Detections;
using Collector.Detection.Rules.Expressions;
using Collector.Detection.Rules.Expressions.Operators;
using Collector.Detection.Rules.Expressions.Predicates;
using Collector.Detection.Rules.Extensions;
using Detection.Yaml;
using Parlot;
using Shared;
using Constants = Collector.Detection.Rules.Builders.Constants;

namespace Collector.Detection.Rules.Parsers;

internal readonly ref struct RuleParser
{
    private const string All = "all";
    private const char Start = '(';
    private const char End = ')';
    private static readonly Regex WithoutPropertiesWithoutDimensionsAggregation = new("(.*)\\s+\\|\\s+count\\(\\)\\s+(==|>=|>|<=|<)\\s+(\\d+)\\s*", RegexOptions.Compiled | RegexOptions.Multiline |  RegexOptions.IgnoreCase);
    private static readonly Regex WithoutPropertiesWithDimensionsAggregation = new("(.*)\\s+\\|\\s+count\\(\\)\\s+by\\s+(\\w+(?:,\\s*\\w+)*)\\s+(==|>=|>|<=|<)\\s+(\\d+)\\s*", RegexOptions.Compiled | RegexOptions.Multiline |  RegexOptions.IgnoreCase);
    private static readonly Regex WithPropertiesWithoutDimensionsAggregation = new("(.*)\\s+\\|\\s+count\\((\\w+)\\)\\s+(==|>=|>|<=|<)\\s+(\\d+)\\s*", RegexOptions.Compiled | RegexOptions.Multiline |  RegexOptions.IgnoreCase);
    private static readonly Regex WithPropertiesWithDimensionsAggregation = new("(.*)\\s+\\|\\s+count\\((\\w+)\\)\\s+by\\s+(\\w+(?:,\\s*\\w+)*)\\s+(==|>=|>|<=|<)\\s+(\\d+)\\s*", RegexOptions.Compiled | RegexOptions.Multiline |  RegexOptions.IgnoreCase);
    private static readonly Regex Condition = new("\\b(?!(?:and|or|not|1 of \\w+[*]?|all of \\w+[*]?)\\b)\\b\\w+\\b[*]?(?=(?:[^'\\\"]*(?:'|\\\")[^'\\\"]*(?:'|\\\"))*[^'\\\"]*$)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex OneOrAll = new("(\\d+|all)\\s+of\\s+(\\w+)(\\*)*", RegexOptions.Compiled | RegexOptions.Multiline |  RegexOptions.IgnoreCase);

    private readonly IList<YamlRule> _yamlRules;
    private readonly Scanner _scanner;
    private readonly ReadOnlySpan<char> _and = "and";
    private readonly ReadOnlySpan<char> _or = "or";
    private readonly ReadOnlySpan<char> _not = "not";
    private readonly Dictionary<string, OneOrAll> _oneOrAllBag = new();
    private readonly IDictionary<string, Aggregation> _aggregationsByHash = new Dictionary<string, Aggregation>();
    private readonly IDictionary<string, IDictionary<string, DetectionExpressions>> _detectionExpressionsById;
    private readonly bool _empty;
    private readonly bool _isCorrelation;

    public RuleParser(IList<YamlRule> yamlRules, IDictionary<string, IDictionary<string, DetectionExpressions>> detectionExpressionsById)
    {
        _yamlRules = yamlRules;
        _detectionExpressionsById = detectionExpressionsById;
        _isCorrelation = yamlRules.Any(rule => rule.IsCorrelation());
        if (_isCorrelation)
        {
            _scanner = new Scanner(FormatCondition(string.Empty));
            _empty = true;
        }
        else if (yamlRules.Single().TryGetConditionNode(out var conditionNode))
        {
            _scanner = new Scanner(FormatCondition(conditionNode));
            _empty = false;
        }
        else
        {
            _scanner = new Scanner(FormatCondition(string.Empty));
            _empty = true;
        }
    }

    private RuleParser(IList<YamlRule> yamlRules, string condition, IDictionary<string, IDictionary<string, DetectionExpressions>> detectionExpressionsById)
    {
        _yamlRules = yamlRules;
        _detectionExpressionsById = detectionExpressionsById;
        if (string.IsNullOrEmpty(condition))
        {
            _scanner = new Scanner(FormatCondition(string.Empty));
            _empty = true;
        }
        else
        {
            _scanner = new Scanner(FormatCondition(condition));
            _empty = false;
        }
    }
    
    private string FormatCondition(string condition)
    {
        var withoutLineBreaks = Regex.Replace(condition, @"\r\n?|\n", " ");
        if (WithPropertiesWithDimensionsAggregation.IsMatch(withoutLineBreaks))
        {
            foreach (Match m in WithPropertiesWithDimensionsAggregation.Matches(withoutLineBreaks))
            {
                var aggregationCondition = m.Groups[1].Value;
                var property = m.Groups[2].Value;
                var dimensions = m.Groups[3].Value;
                var @operator = m.Groups[4].Value;
                var value = m.Groups[5].Value;
                var hash = GetHash();
                _aggregationsByHash[hash] = new Aggregation(aggregationCondition, property, dimensions.Split(Constants.Comma, StringSplitOptions.RemoveEmptyEntries).Select(dimension => dimension.Trim()).ToArray(), @operator, value);
                withoutLineBreaks = withoutLineBreaks.Replace(m.Value, hash);
            }
        }
        
        if (WithPropertiesWithoutDimensionsAggregation.IsMatch(withoutLineBreaks))
        {
            foreach (Match m in WithPropertiesWithoutDimensionsAggregation.Matches(withoutLineBreaks))
            {
                var aggregationCondition = m.Groups[1].Value;
                var property = m.Groups[2].Value;
                var @operator = m.Groups[3].Value;
                var value = m.Groups[4].Value;
                var hash = GetHash();
                _aggregationsByHash[hash] = new Aggregation(aggregationCondition, property, Dimensions: [], @operator, value);
                withoutLineBreaks = withoutLineBreaks.Replace(m.Value, hash);
            }
        }
        
        if (WithoutPropertiesWithDimensionsAggregation.IsMatch(withoutLineBreaks))
        {
            foreach (Match m in WithoutPropertiesWithDimensionsAggregation.Matches(withoutLineBreaks))
            {
                var aggregationCondition = m.Groups[1].Value;
                var dimensions = m.Groups[2].Value;
                var @operator = m.Groups[3].Value;
                var value = m.Groups[4].Value;
                var hash = GetHash();
                _aggregationsByHash[hash] = new Aggregation(aggregationCondition, Property: null, dimensions.Split(Constants.Comma, StringSplitOptions.RemoveEmptyEntries).Select(dimension => dimension.Trim()).ToArray(), @operator, value);
                withoutLineBreaks = withoutLineBreaks.Replace(m.Value, hash);
            }
        }
        
        if (WithoutPropertiesWithoutDimensionsAggregation.IsMatch(withoutLineBreaks))
        {
            foreach (Match m in WithoutPropertiesWithoutDimensionsAggregation.Matches(withoutLineBreaks))
            {
                var aggregationCondition = m.Groups[1].Value;
                var @operator = m.Groups[2].Value;
                var value = m.Groups[3].Value;
                var hash = GetHash();
                _aggregationsByHash[hash] = new Aggregation(aggregationCondition, Property: null, Dimensions: Array.Empty<string>(), @operator, value);
                withoutLineBreaks = withoutLineBreaks.Replace(m.Value, hash);
            }
        }
        
        if (OneOrAll.IsMatch(withoutLineBreaks))
        {
            foreach (Match m in OneOrAll.Matches(withoutLineBreaks))
            {
                _oneOrAllBag.Add(m.Value, new OneOrAll(m.Groups[1].Value.Equals(All, StringComparison.OrdinalIgnoreCase) ? Selection.All : Selection.One, m.Groups[2].Value));
                withoutLineBreaks = withoutLineBreaks.Replace(m.Value, $@"""{m.Value}""");
            }
        }

        if (Condition.IsMatch(withoutLineBreaks))
        {
            foreach (Match m in Condition.Matches(withoutLineBreaks))
            {
                if (_oneOrAllBag.ContainsKey(m.Value)) continue;
                string result = Regex.Replace(withoutLineBreaks, $@"\b{m.Value}\b", $@"""{m.Value}""");
                withoutLineBreaks = result;
            }
        }
        
        return withoutLineBreaks.Replace(@"""""", @"""");
    }

    private static ISet<string> GetProperties(Aggregation aggregation)
    {
        var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (aggregation.Property is not null)
        {
            properties.Add(aggregation.Property);
        }

        foreach (var dimension in aggregation.Dimensions)
        {
            properties.Add(dimension);
        }

        return properties;
    }
    
    public BuildableExpression Parse(RuleMetadata? ruleMetadata = null)
    {
        if (_isCorrelation)
        {
            var correlationRule = _yamlRules.Single(rule => rule.IsCorrelation());
            var rules = correlationRule.GetCorrelationRules().ToHashSet(StringComparer.OrdinalIgnoreCase);
            Expression<Func<WinEvent, bool>> expression = PredicateBuilder.New<WinEvent>(defaultExpression: true);
            
            var yamlRules = _yamlRules.Where(rule => !rule.IsCorrelation() && (rules.Contains(rule.Name) || rules.Contains(rule.Id)));
            foreach (var yamlRule in yamlRules)
            {
                var ruleParser = new RuleParser(yamlRules: new List<YamlRule> { yamlRule }, _detectionExpressionsById);
                var buildableExpression = ruleParser.Parse(yamlRule.ToMetadata());
                expression = expression.And(buildableExpression.BuildPredicateExpression());
            }

            var aggregation = new Aggregation(Condition: null, correlationRule.GetCorrelationProperty(), correlationRule.GetCorrelationDimensions(), correlationRule.GetCorrelationOperator(out var value), value);
            return new AggregationBuildableExpression(expression, aggregate: () => aggregation.Aggregate(correlationRule), GetProperties(aggregation));
        }
        else
        {
            var yamlRule = _yamlRules.Single();
            var expression = ParseCore(yamlRule);
            if (ruleMetadata.HasValue && ruleMetadata.Value.CorrelationOrAggregationTimeSpan.HasValue && expression is not AggregationBuildableExpression)
            {
                var aggregation = new Aggregation(null, null, [], Constants.GreaterThanOrEqual, "0");
                return new AggregationBuildableExpression(expression.BuildPredicateExpression(), aggregate: () => aggregation.Aggregate(yamlRule), GetProperties(aggregation));
            }

            return expression;
        }
    }

    private BuildableExpression ParseCore(YamlRule yamlRule)
    {
        if (_empty)
        {
            BuildableExpression combinedBuildableExpression = new ReducedBuildableExpression(_ => true);
            foreach (var pair in _detectionExpressionsById[yamlRule.Id])
            {
                combinedBuildableExpression = new AndBuildableExpression(new ReducedBuildableExpression(pair.Value.ReducedExpression), combinedBuildableExpression);
            }

            return combinedBuildableExpression;
        }
        else
        {
            return ParseExpression(yamlRule);
        }
    }

    private BuildableExpression ParseExpression(YamlRule yamlRule, bool negate = false)
    {
        var buildableExpression = negate ? new NegateBuildableExpression(ParseMonadicExpression(yamlRule)) : ParseMonadicExpression(yamlRule);
        while (true)
        {
            _scanner.SkipWhiteSpace();
            if (_scanner.ReadText(_and, StringComparison.OrdinalIgnoreCase))
            {
                _scanner.SkipWhiteSpace();
                buildableExpression = new AndBuildableExpression(buildableExpression, ParseMonadicExpression(yamlRule));
            }
            else if (_scanner.ReadText(_or, StringComparison.OrdinalIgnoreCase))
            {
                _scanner.SkipWhiteSpace();
                buildableExpression = new OrBuildableExpression(buildableExpression, ParseMonadicExpression(yamlRule));
            }
            else if (_scanner.ReadText(_not, StringComparison.OrdinalIgnoreCase))
            {
                _scanner.SkipWhiteSpace();
                buildableExpression = new NegateBuildableExpression(buildableExpression);
            }
            else
            {
                break;
            }
        }

        return buildableExpression;
    }
    
    private BuildableExpression ParseMonadicExpression(YamlRule yamlRule)
    {
        _scanner.SkipWhiteSpace();
        if (_scanner.ReadChar(Start))
        {
            var expression = ParseExpression(yamlRule);
            if (!_scanner.ReadChar(End))
            {
                throw new ParseException($"Expected {End}", _scanner.Cursor.Position);
            }

            return expression;
        }
        
        if (_scanner.ReadText(_not, StringComparison.OrdinalIgnoreCase))
        {
            return ParseExpression(yamlRule, negate: true);
        }

        if (_scanner.ReadDoubleQuotedString(out var name))
        {
            var extracted = new string(name);
            var withoutDoubleQuotedString = extracted.Replace(Constants.DoubleQuotes, string.Empty);
            var detectionExpressions = _detectionExpressionsById[yamlRule.Id];
            if (detectionExpressions.TryGetValue(withoutDoubleQuotedString, out var expressions))
            {
                return new ReducedBuildableExpression(expressions.ReducedExpression);
            }

            if (_aggregationsByHash.TryGetValue(withoutDoubleQuotedString, out var aggregation))
            {
                if (!string.IsNullOrEmpty(aggregation.Condition))
                {
                    var parser = new RuleParser(_yamlRules, aggregation.Condition, _detectionExpressionsById);
                    var buildableExpression = parser.Parse();
                    var expression = buildableExpression.BuildPredicateExpression();
                    return new AggregationBuildableExpression(expression, aggregate: () => aggregation.Aggregate(yamlRule), GetProperties(aggregation));
                }
            }
            
            if (_oneOrAllBag.TryGetValue(withoutDoubleQuotedString, out var oneOrAll))
            {
                return GetOneOrAllExpression(detectionExpressions, oneOrAll);
            }
        }

        throw new ParseException("Expected monadic expression", _scanner.Cursor.Position);
    }
    
    private ReducedBuildableExpression GetOneOrAllExpression(IDictionary<string, DetectionExpressions> detectionExpressionsByName, OneOrAll oneOrAll)
    {
        var reducedExpressionsByDetectionName = detectionExpressionsByName.Where(pair => pair.Key.StartsWith(oneOrAll.DetectionName)).GroupBy(pair => pair.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Select(pair => pair.Value.ReducedExpression).Aggregate((left, right) => left.And(right)));
        if (oneOrAll.Selection == Selection.All)
        {
            return new ReducedBuildableExpression(reducedExpressionsByDetectionName.Select(pair => pair.Value).Aggregate((left, right) => left.And(right)));
        }
        
        if (oneOrAll.Selection == Selection.One)
        {
            return new ReducedBuildableExpression(reducedExpressionsByDetectionName.Select(pair => pair.Value).Aggregate((left, right) => left.Or(right)));
        }
        
        throw new ArgumentOutOfRangeException($"{oneOrAll.Selection} is not supported");
    }
    
    private static string GetHash() => Guid.NewGuid().ToString()[..8];
}