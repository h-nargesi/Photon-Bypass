using System.Text.Json;
using Moq;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockFreeRadius;

internal class NasRepositoryMoq : Mock<INasRepository>, IOutSourceMoq
{
    public NasRepositoryMoq() : this(FilePath)
    {
    }

    protected NasRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        var data = JsonSerializer.Deserialize<List<NasEntity>>(raw_text)
                       ?.ToDictionary(x => x.Id)
                   ?? [];

        Setup(x => x.GetAll())
            .Returns(() =>
            {
                var result = data.Values.ToList();

                return Task.FromResult(result);
            });

        Setup(x => x.GetNasInfo(It.IsAny<string>()))
            .Returns<string>(ip =>
            {
                var result = data.Values.FirstOrDefault(x => x.IpAddress == ip);
                return Task.FromResult(result);
            });

        Setup(x => x.GetNasInfo(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(ips =>
            {
                var result = data.Values.Where(x => ips.Contains(x.IpAddress))
                    .ToDictionary(k => k.IpAddress);
                return Task.FromResult(result);
            });
    }

    private const string FilePath = "Data/Radius/nas.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<NasRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<NasRepositoryMoq>().Object);
    }
}