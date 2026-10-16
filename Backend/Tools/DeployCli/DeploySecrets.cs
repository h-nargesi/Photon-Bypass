using System.Text.Json;

namespace PhotonBypass.DeployCli;

/// <summary>
/// Reads <c>LocalDatabaseOptions:ConnectionString</c> from a local copy of the
/// Plesk <c>private/secrets.json</c> (docs/19). The value is a production
/// secret: it is returned to the caller and never written to output or logs.
/// </summary>
public static class DeploySecrets
{
    public const string ConnectionStringKey = "LocalDatabaseOptions:ConnectionString";

    public static string GetConnectionString(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Secrets file not found.", path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        if (!document.RootElement.TryGetProperty("LocalDatabaseOptions", out var section)
            || !section.TryGetProperty("ConnectionString", out var property)
            || property.GetString() is not { Length: > 0 } connectionString)
        {
            throw new InvalidOperationException(
                $"Key '{ConnectionStringKey}' is missing or empty in '{path}'.");
        }

        return connectionString;
    }
}
