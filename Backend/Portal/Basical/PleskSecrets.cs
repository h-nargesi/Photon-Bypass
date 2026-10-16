using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace PhotonBypass.Portal.Basical;

public static class PleskSecrets
{
    public static IReadOnlyList<string> SecretFileCandidates { get; } =
    [
        "../private/secrets.json",
        "../../private/secrets.json"
    ];

    public static WebApplicationBuilder AddPleskPrivateSecrets(this WebApplicationBuilder builder)
    {
        var content_root = builder.Environment.ContentRootPath;

        foreach (var candidate in SecretFileCandidates)
        {
            var path = Path.GetFullPath(Path.Combine(content_root, candidate));

            if (!File.Exists(path))
            {
                continue;
            }

            builder.Configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
            return builder;
        }

        return builder;
    }
}
