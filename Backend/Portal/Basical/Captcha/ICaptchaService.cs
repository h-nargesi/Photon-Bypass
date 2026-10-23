namespace PhotonBypass.Portal.Basical.Captcha;

public interface ICaptchaService
{
    /// <summary>Issues a fresh single-use captcha and caches its answer.</summary>
    CaptchaResult New();

    /// <summary>
    /// Consumes the captcha (single-use: removed whether the code matches or
    /// not). Returns false for a missing/expired id or a wrong code.
    /// </summary>
    bool Validate(string? id, string? code);

    /// <summary>True when the IP has enough anonymous-auth failures to be gated.</summary>
    bool IsRequired(string ip);

    /// <summary>Counts one failure and refreshes the failure window.</summary>
    void NoteFailure(string ip);

    /// <summary>A successful attempt clears the gate for the IP.</summary>
    void NoteSuccess(string ip);
}

public class CaptchaResult
{
    public string Id { get; init; } = string.Empty;

    /// <summary>PNG render as a data-url, ready for an &lt;img src&gt;.</summary>
    public string Image { get; init; } = string.Empty;

    public int ExpiresInSeconds { get; init; }
}
