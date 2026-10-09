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
}
