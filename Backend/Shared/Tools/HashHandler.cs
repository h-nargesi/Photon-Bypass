using System.Security.Cryptography;
using System.Text;

namespace PhotonBypass.Tools;

public static class HashHandler
{
    private static readonly Random Random = new();
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string HashPassword(string plain_text)
    {
        return Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(plain_text)));
    }

    public static string GenerateHashCode(int length = 10)
    {
        var captcha_array = new char[length];
        for (var i = 0; i < length; i++)
        {
            captcha_array[i] = Chars[Random.Next(Chars.Length)];
        }
        return new string(captcha_array);
    }
}
