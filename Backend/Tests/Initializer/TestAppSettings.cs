using Microsoft.Extensions.Configuration;

namespace PhotonBypass.Test.Initializer;

/// <summary>
/// Every value the test host owns lives in <c>Backend/Tests/appsettings.json</c>.
/// Portal's <c>appsettings.Development.json</c> is loaded first (content root,
/// or the copy pulled into the test output) and must not win those keys.
/// </summary>
internal static class TestAppSettings
{
    private static readonly string FilePath =
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static IReadOnlyDictionary<string, string?> Read()
    {
        var file = new ConfigurationBuilder()
            .AddJsonFile(FilePath, optional: false, reloadOnChange: false)
            .Build();

        return file.AsEnumerable()
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static void Apply(IConfigurationBuilder configuration)
    {
        configuration.AddInMemoryCollection(Read());
    }
}
