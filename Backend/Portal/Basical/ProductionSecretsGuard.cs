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
