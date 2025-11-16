using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Collector.Detection.Rules.Builders.Executors.Helpers;

internal static class Base64Helper
{
    public static bool TryGetBase64String(string value, [MaybeNullWhen(false)] out string result)
    {
        result = null;
        // Minimum length that is guaranteed to fit all the data.
        // We don't need the exact length, because the pool
        // might return a larger buffer anyway.
        var minLength = ((value.Length * 3) + 3) / 4;
        var buffer = ArrayPool<byte>.Shared.Rent(minLength);
        try
        {
            if (Convert.TryFromBase64String(value, buffer, out var bytesWritten))
            {
                result = Encoding.UTF8.GetString(buffer, 0, bytesWritten);
                return true;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return false;
    } 
}