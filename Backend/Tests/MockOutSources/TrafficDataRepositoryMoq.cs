using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.MockOutSources.Models;
using PhotonBypass.Tools;

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
        var data_list = JsonSerializer.Deserialize<List<TrafficDataEntity>>(raw_text)
                   ?? [];

        Setup(x => x.Fetch( It.IsAny<DateTime>()))
            .Returns<DateTime>(from =>
            {
                var filtered_list = data_list.Where(traffic => traffic.StartSession >= from)
                    .ToList();
                
                OnFetch?.Invoke(filtered_list);

                return Task.FromResult(filtered_list);
            });

        Setup(x => x.Fetch(It.IsNotNull<int>(), It.IsAny<DateTime>()))
            .Returns<int, DateTime>((account_id, from) =>
            {
                var filtered_list = data_list.Where(traffic => traffic.StartSession >= from && account_id == traffic.AccountId)
                    .ToList();
                
                OnFetch?.Invoke(filtered_list);

                return Task.FromResult(filtered_list);
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
        services.AddTransient<TrafficDataRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<TrafficDataRepositoryMoq>().Object);
    }
}