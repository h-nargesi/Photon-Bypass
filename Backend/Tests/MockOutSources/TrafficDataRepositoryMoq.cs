using Moq;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

class TrafficDataRepositoryMoq : Mock<ITrafficDataRepository>, IOutSourceMoq
{
    public List<TrafficDataEntity> Data { get; private set; } = null!;

    public TrafficDataRepositoryMoq Setup(IDataSource source)
    {
        var raw_text = File.ReadAllText(source.FilePath);
        Data = System.Text.Json.JsonSerializer.Deserialize<List<TrafficDataEntity>>(raw_text)
            ?? [];

        return this;
    }

    IOutSourceMoq IOutSourceMoq.Setup(IDataSource source)
    {
        return Setup(source);
    }

    public class DataSource : IDataSource
    {
        public string FilePath { get; set; } = "Data/traffic-data.json";
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped(s => new DataSource());
        services.AddScoped(s => new TrafficDataRepositoryMoq().Setup(s.GetRequiredService<DataSource>()));
        services.AddLazyScoped(s => s.GetRequiredService<TrafficDataRepositoryMoq>().Object);
    }
}
