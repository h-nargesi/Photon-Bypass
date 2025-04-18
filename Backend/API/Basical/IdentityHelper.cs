using System.Security.Cryptography;

namespace PhotonBypass.API.Basical;

public static class IdentityHelper
{
    private static string? token_key;
    public static string TokenKey => token_key ??= GenerateSecureKey(256);

    private static string GenerateSecureKey(int keySizeInBits = 256)
    {
        using var rng = RandomNumberGenerator.Create();

        byte[] keyBytes = new byte[keySizeInBits / 8];
        rng.GetBytes(keyBytes);

        return Convert.ToBase64String(keyBytes);
    }
}
