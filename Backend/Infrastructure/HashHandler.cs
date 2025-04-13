using System.Security.Cryptography;
using System.Text;

namespace PhotonBypass.Infra;

public static class HashHandler
{
    private static readonly Random random = new();
    private const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string HashPassword(string plain_text)
    {
        return Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(plain_text)));
    }

    public static string GewnerateHashCode(int length)
    {
        var captchaArray = new char[length];
        for (int i = 0; i < length; i++)
        {
            captchaArray[i] = CHARS[random.Next(CHARS.Length)];
        }
        return new string(captchaArray);
    }

}
