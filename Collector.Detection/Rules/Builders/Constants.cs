namespace Collector.Detection.Rules.Builders;

public static class Constants
{
    public static readonly HashSet<string> NestedKeywords = new(StringComparer.Ordinal)
    {
        "value",
        "min_length"
    };

    public const string Attributes = "_attributes.";
    public const string Channel = "Channel";
    public const string Provider = "Provider_Name";
    public const string EventId = "EventID";
    
    public const string Condition = "condition";
    public const string Timeframe = "timeframe";
    public const string Timespan = "timespan";
    public const string Rules = "rules";
    public const string GroupBy = "group-by";
    public const string Field = "field";
    public const string Gte = "gte";
    public const string Gt = "gt";
    public const string Lte = "lte";
    public const string Lt = "lt";
    
    public const string Null = "null";

    public const char Dash = '-';
    public const string HyphenString = "-";
    public const string EnDashString = "–";
    public const string EmDashString = "—";
    public const string HorizontalBarString = "―";
    public const char Slash = '/';
    public const string SlashString = "/";
    public const char Comma = ',';

    public const char AbnormalSeparator = '\u03F4';

    #region Keywords

    public const string MinLength = "min_length";

    #endregion
    
    #region Regex

    public const string StarString = "*";
    public const string QuestionMarkString = "?";
    public const string AnyCharacter = ".*";
    public const string ZeroOrOneCharacter = ".";

    #endregion

    public const string DoubleQuotes = "\"";
    public const char Dot = '.';
    public const string DotString = ".";
    public const char DetailSeparator = '\u00a6';
    public const string SemicolonString = ":";
    
    #region Modifiers

    public const char Pipe = '|';
    public const string PipeString = "|";
    public const string All = "|all";
    public const string Base64 = "base64offset|contains";
    public const string Cased = "cased";
    public const string Cidr = "cidr";
    public const string Contains = "contains";
    public const string ContainsCased = "contains|cased";
    public const string ContainsWindash = "contains|windash";
    public const string ContainsAll = "contains|all";
    public const string ContainsAllWindash = "contains|all|windash";
    public const string EndsWith = "endswith";
    public const string EndsWithCased = "endswith|cased";
    public const string EndsWithWindash = "endswith|windash";
    public const string Exists = "exists";
    public const string EqualsField = "equalsfield";
    public const string EndsWithField = "endswithfield";
    public const string FieldRef = "fieldref";
    public const string FieldRefContains = "fieldref|contains";
    public const string FieldRefEndsWith = "fieldref|endswith";
    public const string FieldRefStartsWith = "fieldref|startswith";
    public const string Re = "re";
    public const string ReI = "re|i";
    public const string ReM = "re|m";
    public const string ReS = "re|s";
    public const string StartsWith = "startswith";
    public const string StartsWithCased = "startswith|cased";
    public const string Expand = "expand";

    #endregion
    
    public const string Equal = "==";
    public const string GreaterThanOrEqual = ">=";
    public const string GreaterThan = ">";
    public const string LessThanOrEqual = "<=";
    public const string LessThan = "<";
}