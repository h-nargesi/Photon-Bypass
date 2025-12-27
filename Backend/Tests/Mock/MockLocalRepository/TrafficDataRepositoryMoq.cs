using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class TrafficDataRepositoryMoq : Mock<ITrafficDataRepository>, IUnitLevelService
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

        Setup(x => x.FetchOpen())
            .Returns(() =>
            {
                var result = data_list.Where(t => t.EndSession == null)
                    .ToList();

                return Task.FromResult(result);
            });

        Setup(x => x.LastUpdateTime())
            .Returns(() =>
            {
                var result = data_list.GroupBy(traffic => traffic.NasId)
                    .Select(group => new
                    {
                        group.Key,
                        LastStart = group.Max(traffic => (DateTime?)traffic.StartSession),
                        MinOpenStart = group.Where(t => t.EndSession == null).Min(t => (DateTime?)t.StartSession),
                    })
                    .ToDictionary(g => g.Key, g => g.MinOpenStart ?? g.LastStart);

                return Task.FromResult(result);
            });

        Setup(x => x.Save(It.IsNotNull<IEnumerable<TrafficDataEntity>>()))
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