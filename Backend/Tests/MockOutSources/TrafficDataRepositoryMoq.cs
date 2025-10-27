using Moq;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Test.MockOutSources.Models;
using PhotonBypass.Tools;
using System.Text.Json;

namespace PhotonBypass.Test.MockOutSources;

internal class TrafficDataRepositoryMoq : Mock<ITrafficDataRepository>, IOutSourceMoq
{
    public event Action<List<TrafficDataEntity>>? OnFetch;

    public event Action<IEnumerable<TrafficDataEntity>>? OnBachSave;

    public TrafficDataRepositoryMoq() : this(FilePath)
    {
    }

    protected TrafficDataRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        var data = JsonSerializer.Deserialize<List<TrafficDataEntity>>(raw_text)
                   ?? [];

        Setup(x => x.Fetch(It.IsNotNull<string>(), It.IsAny<DateTime>()))
            .Returns(() =>
            {
                OnFetch?.Invoke(data);

                return Task.FromResult(data);
            });

        Setup(x => x.BachSave(It.IsNotNull<IEnumerable<TrafficDataEntity>>()))
            .Returns<IEnumerable<TrafficDataEntity>>(list =>
            {
                OnBachSave?.Invoke(list);

                return Task.CompletedTask;
            });
    }

    private const string FilePath = "Data/traffic-data.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<TrafficDataRepositoryMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<TrafficDataRepositoryMoq>().Object);
    }
}