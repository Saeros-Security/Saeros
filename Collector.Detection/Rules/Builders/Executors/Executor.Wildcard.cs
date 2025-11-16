using System.Buffers;
using System.Text.RegularExpressions;
using Collector.Detection.Rules.Builders.Executors.Helpers;

namespace Collector.Detection.Rules.Builders.Executors;

internal static partial class Executor
{
    private static bool TryWildcard(string expectedValue, string value, RegexOptions regexOptions, out bool result, Func<string, string>? patternModifier = null)
    {
        result = false;
        var indexOfWildcard = IndexOfWildcard(expectedValue);
        if (indexOfWildcard >= 0 && IndexOfWildcard(value) == -1)
        {
            result = Regex.IsMatch(input: value, pattern: patternModifier == null ? Escape(expectedValue) : patternModifier(Escape(expectedValue)), regexOptions);
            return true;
        }

        return false;
    }

    private static string Escape(string input)
    {
        var indexOfMetachar = IndexOfMetachar(input.AsSpan());
        return indexOfMetachar < 0 ? input.Replace(Constants.StarString, Constants.AnyCharacter).Replace(Constants.QuestionMarkString, Constants.ZeroOrOneCharacter) : EscapeImpl(input.AsSpan(), indexOfMetachar).Replace(Constants.StarString, Constants.AnyCharacter).Replace(Constants.QuestionMarkString, Constants.ZeroOrOneCharacter);
    }

    private static readonly SearchValues<char> AllEscapingChars = SearchValues.Create("\t\n\f\r #$()+.[\\^{|"); // Escape everything but * and ?
    private static readonly SearchValues<char> WildcardEscapingChars = SearchValues.Create("*?");
    private static int IndexOfMetachar(ReadOnlySpan<char> input) => input.IndexOfAny(AllEscapingChars);
    private static int IndexOfWildcard(ReadOnlySpan<char> input) => input.IndexOfAny(WildcardEscapingChars);
    private const int EscapeMaxBufferSize = 256;
    private static string EscapeImpl(ReadOnlySpan<char> input, int indexOfMetachar)
    {
        var vsb = input.Length <= EscapeMaxBufferSize / 3 ? new ValueStringBuilder(stackalloc char[EscapeMaxBufferSize]) : new ValueStringBuilder(input.Length + 200);
        while (true)
        {
            vsb.Append(input.Slice(0, indexOfMetachar));
            input = input.Slice(indexOfMetachar);

            if (input.IsEmpty)
            {
                break;
            }

            var ch = input[0];
            switch (ch)
            {
                case '\n':
                    ch = 'n';
                    break;
                case '\r':
                    ch = 'r';
                    break;
                case '\t':
                    ch = 't';
                    break;
                case '\f':
                    ch = 'f';
                    break;
            }

            vsb.Append('\\');
            vsb.Append(ch);
            input = input.Slice(1);

            indexOfMetachar = IndexOfMetachar(input);
            if (indexOfMetachar < 0)
            {
                indexOfMetachar = input.Length;
            }
        }

        return vsb.ToString();
    }
}