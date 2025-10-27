using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

internal class NasRepositoryMoq : Mock<INasRepository>, IOutSourceMoq
{
    public NasRepositoryMoq() : this(FilePath)
    {
    }

    protected NasRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
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

    private const string FilePath = "Data/nas.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<NasRepositoryMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<NasRepositoryMoq>().Object);
    }
}