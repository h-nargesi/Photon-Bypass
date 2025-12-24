using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Infra.Repository.DbContext;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.Mock.MockOptions;

internal partial class LocalDapperOptionsMoq : Mock<IOptions<LocalDapperOptions>>, IOptionsMoq
{
    private string connection_string;

    public LocalDapperOptionsMoq(IConfiguration configuration)
    {
        connection_string = configuration["LocalDatabaseOptions:ConnectionString"]
                                ?? throw new Exception("LocalDatabaseOptions:ConnectionString was not set.");

        var parametrized = DatabaseSelector().Replace(connection_string, " {Database};");

        Setup(options => options.Value).Returns(() => new LocalDapperOptions
        {
            ConnectionString = Database == null ? connection_string :
                parametrized.Replace(" {Database};", $" Database={Database};")
        });
    }

    public string RawConnectionString =>
        connection_string = DatabaseSelector().Replace(connection_string, string.Empty);

    public string? Database { get; set; }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<LocalDapperOptionsMoq>();
        services.AddScoped(p => p.GetRequiredService<LocalDapperOptionsMoq>().Object);
    }

    [GeneratedRegex(@" Database=\w+;")]
    private static partial Regex DatabaseSelector();
}