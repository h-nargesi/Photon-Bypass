using System.Security.Cryptography;
using System.Text;

namespace PhotonBypass.Tools;

public static class HashHandler
{
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string EasyChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz123456789";

    public static string GenerateHashCode(int length = 10, bool full = false)
    {
        var collection = full ? Chars : EasyChars;

        return new string(RandomNumberGenerator.GetItems<char>(collection, length));
    }

    /// <summary>
    /// URL-safe one-time code (unpadded Base64Url of 32 random bytes, ~43
    /// chars) for the Telegram deep-link start parameter, which caps at 64
    /// URL-safe characters — GenerateHashCode's charset is not URL-safe.
    /// </summary>
    public static string GenerateUrlSafeCode()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
