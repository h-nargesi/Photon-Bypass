using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PhotonBypass.Portal.Basical;

/// <summary>
/// Presence-based startup guard: the required keys must exist in every
/// environment (base <c>appsettings.json</c> carries no secrets — test
/// fixtures live in <c>appsettings.Development.json</c>). No committed-fixture
/// value matching: that proved fragile (a stale fixture silently kills the
/// rule) and presence is the actual invariant.
/// </summary>
public static class RequiredSettingsGuard
{
    private static readonly string[] RequiredKeys =
    [
        "Issuer:Code",
        "LocalDatabaseOptions:ConnectionString",
    ];

    // email is the main recovery channel while the social channel is off,
    // so an enabled, fully configured transport is mandatory in production;
    // Development keeps the Enabled=false logged no-op (Docs/14 M48)
    private static readonly (string Key, Func<string?, bool> IsMissing)[] EmailRules =
    [
        ("EmailOptions:Enabled", value => !bool.TryParse(value, out var enabled) || !enabled),
        ("EmailOptions:Address", string.IsNullOrWhiteSpace),
        ("EmailOptions:Password", string.IsNullOrWhiteSpace),
    ];

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var offenders = RequiredKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToList();

        if (!environment.IsDevelopment())
        {
            offenders.AddRange(EmailRules
                .Where(rule => rule.IsMissing(configuration[rule.Key]))
                .Select(rule => rule.Key));
        }

        if (offenders.Count == 0)
        {
            return;
        }

        var remediation = environment.IsDevelopment()
            ? "set them in 'Backend/Portal/appsettings.Development.json'"
            : $"put real production values in a 'secrets.json' inside the Plesk vhost 'private' folder (expected next to the content root as {string.Join(" or ", PleskSecrets.SecretFileCandidates)})";

        throw new InvalidOperationException(
            $"Startup blocked by {nameof(RequiredSettingsGuard)}: these required keys are missing [{string.Join(", ", offenders)}]. " +
            $"{remediation}; the secrets file overrides appsettings.json per key. See Docs/19-deploy-runbook.md.");
    }
}
