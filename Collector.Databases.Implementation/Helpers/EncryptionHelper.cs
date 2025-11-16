using System.Security.Cryptography;
using System.Text;

namespace Collector.Databases.Implementation.Helpers;

internal static class EncryptionHelper
{
    private static string _1()
    {
        IEnumerable<char> Enumerate()
        {
            yield return 'd';
            yield return 'g';
            yield return '6';
            yield return 'a';
            yield return '$';
            yield return '5';
            yield return 'n';
            yield return 'B';
            yield return 'd';
            yield return '0';
            yield return 'F';
            yield return 'A';
            yield return 'c';
            yield return 'n';
            yield return 'F';
            yield return 'M';
        }

        var sb = new StringBuilder();
        foreach (var c in Enumerate())
        {
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string _2()
    {
        IEnumerable<char> Enumerate()
        {
            yield return 'Z';
            yield return '3';
            yield return 'G';
            yield return 'H';
            yield return 'j';
            yield return 's';
            yield return 'B';
            yield return 'c';
            yield return '9';
            yield return 'F';
            yield return '2';
            yield return '5';
            yield return 'd';
            yield return '3';
            yield return 'U';
            yield return 'W';
            yield return 'x';
            yield return 'z';
            yield return 'b';
            yield return 'q';
            yield return 'O';
            yield return 'T';
            yield return 'r';
            yield return 'k';
        }

        var sb = new StringBuilder();
        foreach (var c in Enumerate())
        {
            sb.Append(c);
        }

        return sb.ToString();
    }
    
    public static byte[] Encrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(_2());
        aes.IV = Encoding.UTF8.GetBytes(_1());
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        return PerformCryptography(encryptor, data);
    }
    
    public static byte[] Decrypt(byte[] cipher)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(_2());
        aes.IV = Encoding.UTF8.GetBytes(_1());
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        return PerformCryptography(decryptor, cipher);
    }

    private static byte[] PerformCryptography(ICryptoTransform cryptoTransform, byte[] data)
    {
        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write);
        cryptoStream.Write(data, 0, data.Length);
        cryptoStream.FlushFinalBlock();
        return memoryStream.ToArray();
    }
}