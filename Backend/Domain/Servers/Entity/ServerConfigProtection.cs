namespace PhotonBypass.Domain.Servers.Entity;

/// <summary>
/// Marker-prefixed protect/unprotect bridge for <see cref="ServerEntity.JsonConfig"/>.
/// The Domain layer owns no cryptography dependency; the host wires the actual
/// implementation (ASP.NET Data Protection) at startup via the delegates below.
/// When no implementation is wired (e.g. unit tests) values stay plaintext JSON.
/// </summary>
public static class ServerConfigProtection
{
    private const string Prefix = "protected:";

    public static Func<string, string>? ProtectImpl { get; set; }

    public static Func<string, string>? UnprotectImpl { get; set; }

    public static bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string plain)
    {
        var protect = ProtectImpl;
        return protect != null ? $"{Prefix}{protect(plain)}" : plain;
    }

    public static string? Unprotect(string stored)
    {
        if (!IsProtected(stored)) return stored;

        var unprotect = UnprotectImpl;
        if (unprotect == null) return null;

        try
        {
            return unprotect(stored[Prefix.Length..]);
        }
        catch
        {
            return null;
        }
    }
}
