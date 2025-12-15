using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockLocalRepository;

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

        Setup(x => x.Fetch(It.IsAny<DateTime>()))
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
                var filtered_list = data_list
                    .Where(traffic => traffic.StartSession >= from && account_id == traffic.AccountId)
                    .ToList();

                OnFetch?.Invoke(filtered_list);

                return Task.FromResult(filtered_list);
            });

        Setup(x => x.Fetch(It.IsNotNull<IEnumerable<int>>(), It.IsAny<DateTime>()))
            .Returns<IEnumerable<int>, DateTime>((nas_ids, from) =>
            {
                var mask = nas_ids.ToHashSet();

                var filtered_list = data_list
                    .Where(traffic => traffic.StartSession >= from && mask.Contains(traffic.NasId))
                    .GroupBy(k => k.NasId)
                    .ToDictionary(k => k.Key, v => v.ToList());

                return Task.FromResult(filtered_list);
            });

        Setup(x => x.LastUpdateTime())
            .Returns(() =>
            {
                var last_update_time = data_list
                    .Where(traffic => !traffic.EndSession.HasValue)
                    .Min(traffic => (DateTime?)traffic.StartSession);

                if (last_update_time.HasValue)
                {
                    return Task.FromResult(last_update_time);
                }

                last_update_time = data_list
                    .Max(traffic => traffic.StartSession);

                return Task.FromResult(last_update_time);
            });

        Setup(x => x.BachSave(It.IsNotNull<IEnumerable<TrafficDataEntity>>()))
            .Returns<IEnumerable<TrafficDataEntity>>(list =>
            {
                OnBachSave?.Invoke(list);
                return Task.CompletedTask;
            });
    }

    private const string FilePath = "Data/Local/traffic-data.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<TrafficDataRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<TrafficDataRepositoryMoq>().Object);
    }
}