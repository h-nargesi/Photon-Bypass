using System.Security.Cryptography;
using System.Text;

namespace PhotonBypass.Tools;

public static class PasswordHasher
{
    private const string Algorithm = "PBKDF2";
    private const string Version = "v1";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000;

    public static string Hash(string plain)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plain), salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join('$',
            string.Empty, Algorithm, Version, Iterations,
            Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public static bool Verify(string plain, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;

        var parts = stored.Split('$');

        if (parts.Length != 6 || parts[1] != Algorithm || parts[2] != Version) return false;
        if (!int.TryParse(parts[3], out var iterations) || iterations <= 0) return false;

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expected = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plain), salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
