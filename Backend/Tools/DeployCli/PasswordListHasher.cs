using PhotonBypass.Domain.Account.Business;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Tools;

namespace PhotonBypass.DeployCli;

/// <summary>
/// Offline batch hasher for <c>hash-passwords</c>. Every non-empty line is
/// checked with <see cref="AccountBusiness.ValidatePassword"/> before
/// <see cref="PasswordHasher.Hash"/> runs, so a file that fails the portal
/// policy produces no hashes. A zero-length line is kept so output line
/// numbers stay aligned with the file. The password text never appears in
/// <see cref="InvalidLine.Message"/> — only <see cref="UserException.UserMessage"/>.
/// </summary>
public static class PasswordListHasher
{
    public readonly record struct InvalidLine(int LineNumber, string Message);

    /// <summary>
    /// <see cref="Hashes"/> is empty unless <see cref="Succeeded"/> is true.
    /// On success its length matches the input and a null entry is a blank line.
    /// A file with no non-empty line fails with an empty <see cref="InvalidLines"/>.
    /// </summary>
    public sealed record HashBatch(
        bool Succeeded,
        IReadOnlyList<string?> Hashes,
        IReadOnlyList<InvalidLine> InvalidLines);

    public static HashBatch Hash(IReadOnlyList<string> lines)
    {
        var invalid = new List<InvalidLine>();
        var hasPassword = false;

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }

            hasPassword = true;

            try
            {
                AccountBusiness.ValidatePassword(lines[i]);
            }
            catch (UserException exception)
            {
                invalid.Add(new InvalidLine(i + 1, exception.UserMessage ?? "password does not meet the portal policy"));
            }
        }

        if (invalid.Count > 0 || !hasPassword)
        {
            return new HashBatch(false, [], invalid);
        }

        var hashes = new string?[lines.Count];

        for (var i = 0; i < lines.Count; i++)
        {
            hashes[i] = lines[i].Length == 0 ? null : PasswordHasher.Hash(lines[i]);
        }

        return new HashBatch(true, hashes, []);
    }
}
