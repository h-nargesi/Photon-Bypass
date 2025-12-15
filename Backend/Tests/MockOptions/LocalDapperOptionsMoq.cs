using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Test.MockOptions;

internal class LocalDapperOptionsMoq : Mock<IOptions<LocalDapperOptions>>, IOutSourceMoq, IOptionsMoq
{
    public const string DatabaseStructureInitializerFilePath = "../../Database/LocalDatabase/";
    public const string DatabaseDataInitializerFilePath = "Data/Sql/";

    public LocalDapperOptionsMoq()
    {
        Setup(options => options.Value).Returns(new LocalDapperOptions
        {
            ConnectionString = "Server=myServerName\\myInstanceName;Database=myDataBase;User Id=myUsername;Password=myPassword;"
        });
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton(new LocalDapperOptionsMoq().Object);
    }
}