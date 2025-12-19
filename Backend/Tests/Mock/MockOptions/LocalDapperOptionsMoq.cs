using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Test.Mock.MockOptions;

internal class LocalDapperOptionsMoq : Mock<IOptions<LocalDapperOptions>>
{
    public const string DatabaseStructureInitializerFilePath = "../../Database/LocalDatabase/";
    public const string DatabaseDataInitializerFilePath = "Data/Sql/";

    public LocalDapperOptionsMoq()
    {
        Setup(options => options.Value).Returns(new LocalDapperOptions
        {
            ConnectionString = $"Server={ServerName};Database={DatabaseName};User Id={Username};Password={Password};"
        });
    }

    public string ServerName { get; init; }

    public string DatabaseName { get; init; }

    public string Username { get; init; }

    public string Password { get; init; }
}