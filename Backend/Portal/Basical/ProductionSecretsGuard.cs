using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PhotonBypass.Portal.Basical;

public static class CommittedTestSecretFixtures
{
    public const string JwtSigningKey =
        "aGFyZHBlbmNpbGRpdmlkZXN1ZGRlbmx5bG9vc2Vkb25lYWlycGxhbmVmdXR1cmVvcmJpdGNvcHBlcndvbmV2ZW50dWFsbHlkZXBl" +
        "bmRleHBsYWludG93bmV4cHJlc3Npb25hcmV0cmFpbGRlY2xhcmVkbGVzc29ucGlsZW5vaXNlc3RhdGVpc3J1bGVyZGV0ZXJtaW5l" +
        "c3RyZWFtdmVyYnNoZWV0bGFrZW1vdGlvbmRyb3ZldGVsbHB1YmxpY3RhbGVzYWZmZWN0bm9vZmZ0ZWV0aGJlY2F1c2Vmb3VnaHRm" +
        "YXJzd2luZ2JlbGxzdXBwbHlmdW5sZWdwdXR0aW5ndGFzdGV3YXRlcmFjY3VyYXRlcGVvcGxlbmVzdGV4cGxvcmVleGNpdGluZ3Nr" +
        "aW53YWl0c2FpbGVhcm5zb2Fwbm9vbnNsb3dseXR5cGVkaXN0YW5jZXRlcm1jb2xvbnlwZXJzb25hbHdpdGhpbndoZWVsZWFydGhp" +
        "bnZlbnRlZGJlY29tZXRoZW1zZWx2ZXNhbmltYWxzb3RpbnlmYWxsZW5mcm9udHJlY2VudGdlbnRseXNvZnRseWdyZXdwaXRjaGdy" +
        "YXNzYmFnc2xhdmVlbGVwaGFudGdyYW5kZmF0aGVyZGl2aWRlZW5qb3lkZXRhaWxuZXN0c3Bpcml0Y291bnQ=";

    public const string RadiusServicePassword = "jt8GRBX5wbuQJY6hZN9MCnHqpFTcPykveAUxSgr3";

    public const string TelegramBotToken = "0000000000:TEST-TELEGRAM-BOT-TOKEN-FIXTURE-0000000000";

    public const string LocalDatabaseMarker = "FastBypass_DbScenario";

    public const string RadDatabaseMarker = "Database=rd;";
}

public static class ProductionSecretsGuard
{
    private static readonly (string Key, Func<string?, bool> IsCommittedTestValue)[] Rules =
    [
        ("Issuer:Code", value => value == CommittedTestSecretFixtures.JwtSigningKey),
        ("LocalDatabaseOptions:ConnectionString", value => value?.Contains(CommittedTestSecretFixtures.LocalDatabaseMarker) == true),
        ("RadDapperOptions:ConnectionString", value => value?.Contains(CommittedTestSecretFixtures.RadDatabaseMarker) == true),
        ("RadiusServiceOptions:Password", value => value == CommittedTestSecretFixtures.RadiusServicePassword),

        // email is the main recovery channel while the social channel is off,
        // so an enabled, fully configured transport is mandatory in production
        ("EmailOptions:Enabled", value => !bool.TryParse(value, out var enabled) || !enabled),
        ("EmailOptions:Address", string.IsNullOrWhiteSpace),
        ("EmailOptions:Password", string.IsNullOrWhiteSpace),

        // telegram adapter: the committed bot token is a test fixture — the
        // real one must come from private/secrets.json
        ("TelegramOptions:BotToken", value => value == CommittedTestSecretFixtures.TelegramBotToken),
    ];

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var offenders = Rules
            .Where(rule => rule.IsCommittedTestValue(configuration[rule.Key]))
            .Select(rule => rule.Key)
            .ToList();

        // channel-conditional completeness (array rules are value-only and
        // cannot see the Channel selection): enabling the Telegram channel
        // requires its whole configuration block
        if (configuration["SocialMediaOptions:Channel"] == "Telegram")
        {
            foreach (var required_key in new[]
                     {
                         "TelegramOptions:BotToken",
                         "TelegramOptions:BotUsername",
                         "TelegramOptions:Proxy:Address",
                         "TelegramOptions:OperationsChatId",
                     })
            {
                if (string.IsNullOrWhiteSpace(configuration[required_key]))
                {
                    offenders.Add(required_key);
                }
            }
        }

        if (offenders.Count == 0)
        {
            return;
        }

        var expected_locations = string.Join(" or ", PleskSecrets.SecretFileCandidates);

        throw new InvalidOperationException(
            $"Production startup blocked by {nameof(ProductionSecretsGuard)}: " +
            $"these keys still carry committed test values [{string.Join(", ", offenders)}]. " +
            $"Put real production values in a 'secrets.json' inside the Plesk vhost 'private' folder " +
            $"(expected next to the content root as {expected_locations}); " +
            $"the file overrides appsettings.json per key. See docs/19-deploy-runbook.md.");
    }
}
