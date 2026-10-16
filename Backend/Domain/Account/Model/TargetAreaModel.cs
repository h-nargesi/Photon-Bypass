namespace PhotonBypass.Domain.Account.Model;

/// <summary>
/// Cached login context of a user (docs/20 D2): the role for authorization
/// checks and the usernames the user may act on behalf of.
/// </summary>
public class TargetAreaModel
{
    public AccountRole Role { get; set; }

    public HashSet<string> Targets { get; set; } = [];
}
