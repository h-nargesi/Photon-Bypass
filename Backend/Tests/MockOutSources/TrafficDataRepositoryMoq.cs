using Moq;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Test.MockOutSources.Models;
using PhotonBypass.Tools;
using System.Text.Json;

namespace PhotonBypass.Test.MockOutSources;

class TrafficDataRepositoryMoq : Mock<ITrafficDataRepository>, IOutSourceMoq
{
    public List<TrafficDataEntity> Data { get; private set; } = null!;

    public event Action<List<TrafficDataEntity>>? OnFetch;

    public event Action<IEnumerable<TrafficDataEntity>>? OnBachSave;

    public TrafficDataRepositoryMoq Setup(IDataSource source)
    {
        var raw_text = File.ReadAllText(source.FilePath)
            .PrepareAllDateTimes();
        Data = JsonSerializer.Deserialize<List<TrafficDataEntity>>(raw_text)
            ?? [];

        Setup(x => x.Fetch(It.IsNotNull<string>(), It.IsAny<DateTime>()))
            .Returns(() =>
            {
                OnFetch?.Invoke(Data);

                return Task.FromResult(Data);
            });

        Setup(x => x.BachSave(It.IsNotNull<IEnumerable<TrafficDataEntity>>()))
            .Returns<IEnumerable<TrafficDataEntity>>(list =>
            {
                OnBachSave?.Invoke(list);

                return Task.CompletedTask;
            });

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
