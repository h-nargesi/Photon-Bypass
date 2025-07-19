using Moq;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

class NasRepositoryMoq : Mock<INasRepository>, IOutSourceMoq
{
    public Dictionary<int, NasEntity> Data { get; private set; } = null!;

    public NasRepositoryMoq Setup(IDataSource source)
    {
        var raw_text = File.ReadAllText(source.FilePath);
        Data = System.Text.Json.JsonSerializer.Deserialize<List<NasEntity>>(raw_text)
            ?.ToDictionary(x => x.Id)
            ?? [];

        Setup(x => x.GetAll())
            .Returns(() =>
            {
                var result = Data.Values.ToList();

                return Task.FromResult(result);
            });

        Setup(x => x.GetNasInfo(It.IsAny<string>()))
            .Returns<string>(ip =>
            {
                var result = Data.Values.FirstOrDefault(x => x.IpAddress == ip);
                return Task.FromResult(result);
            });

        Setup(x => x.GetNasInfo(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(ips =>
            {
                var result = Data.Values.Where(x => ips.Contains(x.IpAddress))
                    .ToDictionary(k => k.IpAddress);
                return Task.FromResult(result);
            });

        return this;
    }

    IOutSourceMoq IOutSourceMoq.Setup(IDataSource source)
    {
        return Setup(source);
    }

    public class DataSource : IDataSource
    {
        public string FilePath { get; set; } = "Data/nas.json";
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped(s => new DataSource());
        services.AddScoped(s => new NasRepositoryMoq().Setup(s.GetRequiredService<DataSource>()));
        services.AddLazyScoped(s => s.GetRequiredService<NasRepositoryMoq>().Object);
    }
}
