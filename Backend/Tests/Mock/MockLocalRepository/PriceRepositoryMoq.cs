using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Static;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

public class PriceRepositoryMoq : Mock<IPriceRepository>, IUnitLevelService
{
    public PriceRepositoryMoq() : this(FilePath)
    {
    }

    protected PriceRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        var data_list = JsonSerializer.Deserialize<List<PriceEntity>>(raw_text)
                              ?? [];

        Setup(x => x.GetActives())
            .Returns(Task.FromResult(data_list));

        var events_moq = new Mock<IEntityEvent<PriceEntity>>();

        Setup(x => x.Events).Returns(events_moq.Object);
    }

    private const string FilePath = "Data/Local/price.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<PriceRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<PriceRepositoryMoq>().Object);
    }
}