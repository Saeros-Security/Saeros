using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using K4os.Compression.LZ4;
using Shared.Helpers;

namespace Collector.Core.Extensions;

public static class StringExtensions
{
    private const int MaxStackSize = 1024;

    [SkipLocalsInit]
    public static byte[] LZ4CompressString(this string input)
    {
        var length = Encoding.UTF8.GetByteCount(input);
        if (length > MaxStackSize)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                var written = Encoding.UTF8.GetBytes(input, buffer);
                return LZ4Pickler.Pickle(buffer.AsSpan()[..written]);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
            }
        }
        else
        {
            Span<byte> buffer = stackalloc byte[length];
            var written = Encoding.UTF8.GetBytes(input, buffer);
            return LZ4Pickler.Pickle(buffer[..written]);
        }
    }
    
    public static long ParseLong(this string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.Parse(value[2..], NumberStyles.HexNumber);
        }

        if (long.TryParse(value, out var parsed)) return parsed;
        throw new ArgumentException($"{value} is not a valid integer");
    }
    
    public static int Parse(this string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(value[2..], NumberStyles.HexNumber);
        }

        if (int.TryParse(value, out var parsed)) return parsed;
        throw new ArgumentException($"{value} is not a valid integer");
    }
    
    public static uint ParseUnsigned(this string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.Parse(value[2..], NumberStyles.HexNumber);
        }

        if (uint.TryParse(value, out var parsed)) return parsed;
        throw new ArgumentException($"{value} is not a valid integer");
    }
    
    public static byte[] RemoveBom(this Span<byte> input)
    {
        ReadOnlySpan<byte> utf8Bom = [0xEF, 0xBB, 0xBF];
        if (input.StartsWith(utf8Bom))
        {
            input = input[utf8Bom.Length..];
        }

        return input.ToArray();
    }
    
    public static string RemoveBom(this string input)
    {
        return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input).AsSpan().RemoveBom());
    }

    public static bool IsKnownSid(this string sid)
    {
        return WellKnownSids.NonDomainSpecificSids.Contains(sid) || sid.StartsWith("S-1-5-90", StringComparison.OrdinalIgnoreCase) || sid.StartsWith("S-1-5-96", StringComparison.OrdinalIgnoreCase);
    }
}