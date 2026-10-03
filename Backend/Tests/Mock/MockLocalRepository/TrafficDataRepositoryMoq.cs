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

    private readonly List<TrafficDataEntity> data_list;

    public IReadOnlyList<TrafficDataEntity> Data => data_list;

    public TrafficDataRepositoryMoq() : this(FilePath)
    {
    }

    protected TrafficDataRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        data_list = JsonSerializer.Deserialize<List<TrafficDataEntity>>(raw_text)
                    ?? [];

        for (var i = 0; i < data_list.Count; i++)
        {
            if (data_list[i].Id < 1) data_list[i].Id = i + 1;
        }

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
                var known_ids = data_list.Select(traffic => traffic.Id).ToHashSet();

                var next_id = known_ids.Count > 0 ? known_ids.Max() : 0;

                foreach (var record in list.Where(record => record.Id < 1))
                {
                    record.Id = ++next_id;
                    data_list.Add(record);
                }

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