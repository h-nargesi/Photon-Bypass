using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Test.Mock.MockOptions;

internal class LocalDapperOptionsMoq : Mock<IOptions<LocalDapperOptions>>, IOptionsMoq
{
    public LocalDapperOptionsMoq(IConfiguration configuration)
    {
        var connection_string = configuration["LocalDatabaseOptions:ConnectionString"]
                                ?? throw new Exception("LocalDatabaseOptions:ConnectionString was not set.");

        Setup(options => options.Value).Returns(() => new LocalDapperOptions
        {
            ConnectionString = connection_string.Replace(" {Database};",
                Database != null ? $" Database={Database};" : string.Empty)
        });
    }

    public string? Database { get; set; }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<LocalDapperOptionsMoq>();
        services.AddScoped(p => p.GetRequiredService<LocalDapperOptionsMoq>().Object);
    }
}