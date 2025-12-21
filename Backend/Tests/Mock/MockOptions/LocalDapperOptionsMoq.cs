using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Test.Mock.MockOptions;

internal class LocalDapperOptionsMoq : Mock<IOptions<LocalDapperOptions>>, IOptionsMoq
{
    public const string DatabaseStructureInitializerFilePath = "../../../../../Database/LocalDatabase/";
    public const string DatabaseDataInitializerFilePath = "Data/Sql/";

    public LocalDapperOptionsMoq()
    {
        Setup(options => options.Value).Returns(() => new LocalDapperOptions
        {
            ConnectionString = $"Server=.;{(Database != null ? $" Database={Database};" : string.Empty)} " +
                               "User Id=sa; Password=ph0t0n-X; Encrypt=False; TrustServerCertificate=True;"
        });
    }

    public string? Database { get; set; }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<LocalDapperOptionsMoq>();
        services.AddScoped(p => p.GetRequiredService<LocalDapperOptionsMoq>().Object);
    }
}