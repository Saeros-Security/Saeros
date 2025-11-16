using Collector.Detection.Rules.Builders;
using Collector.Detection.Rules.Correlations;

namespace Collector.Detection.Rules.Extensions;

internal static class StringExtensions
{
    public static TimeSpan ToTimeframe(this string time)
    {
        if (time.EndsWith('s'))
        {
            return TimeSpan.FromSeconds(int.Parse(time[..^1]));
        }
        
        if (time.EndsWith('m'))
        {
            return TimeSpan.FromMinutes(int.Parse(time[..^1]));
        }
        
        if (time.EndsWith('h'))
        {
            return TimeSpan.FromHours(int.Parse(time[..^1]));
        }
        
        if (time.EndsWith('d'))
        {
            return TimeSpan.FromDays(int.Parse(time[..^1]));
        }
        
        if (time.EndsWith('M'))
        {
            return TimeSpan.FromDays(int.Parse(time[..^1]) * 31 - 1);
        }

        throw new ArgumentException($"Unknown time format: {time}");
    }

    public static Operator ToOperator(this string @operator)
    {
        return @operator switch
        {
            Constants.Equal => Operator.Equal,
            Constants.GreaterThanOrEqual or Constants.Gte => Operator.GreaterThanOrEqual,
            Constants.GreaterThan or Constants.Gt => Operator.GreaterThan,
            Constants.LessThanOrEqual or Constants.Lte => Operator.LessThanOrEqual,
            Constants.LessThan or Constants.Lt => Operator.LessThan,
            _ => throw new ArgumentException($"Unknown operator: {@operator}")
        };
    }

    public static IEnumerable<string> FromAbnormalPattern(this string fieldName)
    {
        return fieldName.Split(Constants.AbnormalSeparator, StringSplitOptions.RemoveEmptyEntries);
    }

    internal readonly ref struct Split(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        public readonly ReadOnlySpan<char> Left = left;
        public readonly ReadOnlySpan<char> Right = right;
    }

    public static Split SplitOnce(this string input, ReadOnlySpan<char> separator)
    {
        return input.AsSpan().SplitOnce(separator);
    }
    
    public static Split SplitOnce(this ReadOnlySpan<char> span, ReadOnlySpan<char> separator)
    {
        var i = 0;
        ReadOnlySpan<char> left = string.Empty;
        ReadOnlySpan<char> right = string.Empty;
        foreach (var range in span.Split(separator))
        {
            var value = span[range];
            if (i == 0)
            {
                left = value;
            }
            else
            {
                var (offset, _) = range.GetOffsetAndLength(span.Length);
                right = span[offset..];
                break;
            }
            
            i++;
        }

        return new Split(left, right);
    }
    
    public static string TakeLast(this string input, char separator)
    {
        var span = input.AsSpan();
        foreach (var range in span.Split(separator))
        {
            var (offset, length) = range.GetOffsetAndLength(span.Length);
            var tail = offset + length == span.Length;
            if (!tail) continue;
            return new string(span[range]);
        }

        return string.Empty;
    }
}