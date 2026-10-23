using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace PhotonBypass.Portal.Basical.Captcha;

/// <summary>
/// IMemoryCache-backed captcha store (single Plesk instance; a distributed
/// cache is only needed if the app ever scales out — deferred by design).
/// Cache contract:
///   Captcha|{id}    → answer (TTL CodeExpirySeconds, Size=1, single-use)
///   CaptchaFail|{ip} → failure count (TTL FailureWindowMinutes, refreshed on
///                      every failure, removed on success)
/// </summary>
internal class CaptchaService(IMemoryCache cache, IOptions<CaptchaOptions> options) : ICaptchaService
{
    /// <summary>Ambiguity-free latin/digit set (no 0,O,1,I,l).</summary>
    private static readonly string[] Charset =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "M",
        "N", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "2", "3", "4", "5", "6", "7", "8", "9",
    ];

    private CaptchaOptions Options => options.Value;

    internal static string CodeKey(string id) => $"Captcha|{id}";

    internal static string FailureKey(string ip) => $"CaptchaFail|{ip}";

    public CaptchaResult New()
    {
        // 16 CSPRNG bytes → Base64; modulo stays unbiased (256 % 32 == 0)
        var id = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var noise = RandomNumberGenerator.GetBytes(Options.CodeLength);
        var code = string.Concat(noise.Select(b => Charset[b % Charset.Length]));

        cache.Set(CodeKey(id), code, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Options.CodeExpirySeconds),
            Size = 1,
        });

        return new CaptchaResult
        {
            Id = id,
            Image = CaptchaImageGenerator.Render(code, Options.ImageWidth, Options.ImageHeight),
            ExpiresInSeconds = Options.CodeExpirySeconds,
        };
    }

    public bool Validate(string? id, string? code)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(code)) return false;

        var key = CodeKey(id);
        var expected = cache.Get(key) as string;

        // single-use: one captcha buys exactly one attempt, match or not
        cache.Remove(key);

        return expected != null
            && string.Equals(expected.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public bool IsRequired(string ip)
    {
        return cache.Get(FailureKey(ip)) is int count && count >= Options.FailureThreshold;
    }

    public void NoteFailure(string ip)
    {
        var key = FailureKey(ip);
        var count = cache.Get(key) is int value ? value : 0;

        // re-Set restarts the window: sliding expiration on each failure
        cache.Set(key, count + 1, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Options.FailureWindowMinutes),
            Size = 1,
        });
    }

    public void NoteSuccess(string ip)
    {
        cache.Remove(FailureKey(ip));
    }
}
