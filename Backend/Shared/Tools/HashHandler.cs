using System.Security.Cryptography;
using System.Text;

namespace PhotonBypass.Tools;

public static class HashHandler
{
    private static readonly Random Random = new();
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string EasyChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz123456789";

    public static string HashPassword(string plain_text)
    {
        return Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(plain_text)));
    }

    public static string GenerateHashCode(int length = 10, bool full = false)
    {
        var collection = full ? Chars : EasyChars;
        var captcha_array = new char[length];
        for (var i = 0; i < length; i++)
        {
            captcha_array[i] = collection[Random.Next(collection.Length)];
        }
        return new string(captcha_array);
    }
}
