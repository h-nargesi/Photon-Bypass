namespace PhotonBypass.DeployCli;

/// <summary>
/// First-release deploy tool (docs/19). Two commands:
///   create-admin — insert the single admin account (docs/20 D2/D9/D11) with a
///                  real password; refuses to overwrite usernames or add a second admin.
///   verify       — smoke-check the stored hash against a password.
/// Connection: --secrets &lt;path&gt; (local copy of private/secrets.json) or the
/// DEPLOY_CONNECTION_STRING environment variable. Password: --password or the
/// DEPLOY_ADMIN_PASSWORD environment variable. Secrets are never printed.
/// </summary>
internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitUsage = 1;
    private const int ExitFailure = 2;

    private const string UsageText = """
        usage: DeployCli <command> [options]

        commands:
          create-admin   insert the first-release admin account (exactly one is allowed)
          verify         verify an admin username/password against the stored hash

        options:
          --username <name>     account username (required)
          --password <value>    account password (alternative: env DEPLOY_ADMIN_PASSWORD)
          --secrets <path>      read LocalDatabaseOptions:ConnectionString from this
                                local copy of private/secrets.json (alternative:
                                env DEPLOY_CONNECTION_STRING)

        exit codes: 0 = ok, 1 = usage error, 2 = failure
        """;

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return ExitFailure;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            return Usage("missing command");
        }

        if (args[0] is not ("create-admin" or "verify"))
        {
            return Usage($"unknown command '{args[0]}'");
        }

        var options = ParseOptions(args[1..]);
        if (options is null)
        {
            return ExitUsage;
        }

        var resolved = ResolveConnection(options);

        return args[0] switch
        {
            "create-admin" => CreateAdmin(resolved),
            "verify" => Verify(resolved),
            _ => ExitUsage,
        };
    }

    private static Options? ParseOptions(string[] args)
    {
        var username = null as string;
        var password = null as string;
        var secrets = null as string;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--username" when Next(args, ref i) is { } value:
                    username = value;
                    break;
                case "--password" when Next(args, ref i) is { } value:
                    password = value;
                    break;
                case "--secrets" when Next(args, ref i) is { } value:
                    secrets = value;
                    break;
                default:
                    Usage($"unknown or incomplete option '{args[i]}'");
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            Usage("--username is required");
            return null;
        }

        password ??= Environment.GetEnvironmentVariable("DEPLOY_ADMIN_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            Usage("password is required (--password or env DEPLOY_ADMIN_PASSWORD)");
            return null;
        }

        return new Options(username, password, secrets);
    }

    private static Resolved ResolveConnection(Options options)
    {
        var connectionString = options.SecretsPath is { } path
            ? DeploySecrets.GetConnectionString(path)
            : Environment.GetEnvironmentVariable("DEPLOY_CONNECTION_STRING")
              ?? throw new InvalidOperationException(
                  "connection is required (--secrets <path> or env DEPLOY_CONNECTION_STRING)");

        return new Resolved(options.Username, options.Password, connectionString);
    }

    private static string? Next(string[] args, ref int index)
    {
        return ++index < args.Length ? args[index] : null;
    }

    private static int CreateAdmin(Resolved options)
    {
        var status = AdminSeeder.CreateAdmin(options.ConnectionString, options.Username, options.Password);

        switch (status)
        {
            case AdminSeeder.CreateStatus.Created:
                Console.WriteLine($"admin account '{options.Username}' created (Role=Admin, AllowSelfService)");
                return ExitOk;
            case AdminSeeder.CreateStatus.DuplicateUsername:
                Console.Error.WriteLine(
                    $"error: an account named '{options.Username}' already exists — refusing to overwrite it");
                return ExitFailure;
            default:
                Console.Error.WriteLine(
                    "error: an admin account already exists — exactly one admin is allowed (docs/20 D9)");
                return ExitFailure;
        }
    }

    private static int Verify(Resolved options)
    {
        if (!AdminSeeder.Verify(options.ConnectionString, options.Username, options.Password))
        {
            Console.Error.WriteLine($"error: verification failed for '{options.Username}'");
            return ExitFailure;
        }

        Console.WriteLine($"verify ok: '{options.Username}' authenticates with the stored hash");
        return ExitOk;
    }

    private static int Usage(string reason)
    {
        Console.Error.WriteLine($"error: {reason}");
        Console.Error.WriteLine(UsageText);
        return ExitUsage;
    }

    private sealed record Options(string Username, string Password, string? SecretsPath);

    private sealed record Resolved(string Username, string Password, string ConnectionString);
}
