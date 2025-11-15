using System.Text;
using K4os.Compression.LZ4;

namespace Collector.Core.Extensions;

public static class ArrayExtensions
{
    public static string LZ4UncompressString(this byte[] input)
    {
        return Encoding.UTF8.GetString(LZ4Pickler.Unpickle(input));
    }
}