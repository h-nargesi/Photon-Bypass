using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Static;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockLocalRepository;

public class PriceRepositoryMoq : Mock<IPriceRepository>, IOutSourceMoq
{
    public PriceRepositoryMoq() : this(FilePath)
    {
    }

    protected PriceRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data_list = JsonSerializer.Deserialize<List<PriceEntity>>(raw_text)
                              ?? [];

        Setup(x => x.GetActives())
            .Returns(Task.FromResult(data_list));
    }

    private const string FilePath = "Data/price.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<PriceRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<PriceRepositoryMoq>().Object);
    }
}