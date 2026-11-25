using PhotonBypass.Tools;

namespace PhotonBypass.DeployCli;

/// <summary>
/// Offline batch hasher for <c>hash-passwords</c>. Every non-empty line is
/// hashed as it is: the portal password policy does not apply to batch
/// passwords and nothing is trimmed. A zero-length line is kept so output
/// line numbers stay aligned with the file.
/// </summary>
public static class PasswordListHasher
{
    /// <summary>
    /// <see cref="Hashes"/> is empty unless <see cref="Succeeded"/> is true.
    /// On success its length matches the input and a null entry is a blank line.
    /// A file with no non-empty line fails.
    /// </summary>
    public sealed record HashBatch(bool Succeeded, IReadOnlyList<string?> Hashes);

    public static HashBatch Hash(IReadOnlyList<string> lines)
    {
        if (lines.All(line => line.Length == 0))
        {
            return new HashBatch(false, []);
        }

        var hashes = new string?[lines.Count];

        for (var i = 0; i < lines.Count; i++)
        {
            hashes[i] = lines[i].Length == 0 ? null : PasswordHasher.Hash(lines[i]);
        }

        return new HashBatch(true, hashes);
    }
}
