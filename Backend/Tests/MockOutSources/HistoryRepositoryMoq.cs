using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

class HistoryRepositoryMoq : Mock<IHistoryRepository>, IOutSourceMoq
{
    Dictionary<string, List<HistoryEntity>> data = null!;

    public event Action<string, DateTime?, DateTime?, List<HistoryEntity>>? OnGetHistory;

    public HistoryRepositoryMoq Setup(IDataSource source)
    {
        var raw_text = File.ReadAllText(source.FilePath);
        data = System.Text.Json.JsonSerializer.Deserialize<List<HistoryEntity>>(raw_text)
            ?.GroupBy(k => k.Target).ToDictionary(x => x.Key, v => v.ToList())
            ?? [];

        Setup(x => x.GetHistory(It.IsNotNull<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns<string, DateTime?, DateTime?>((target, from, to) =>
            {
                if (!data.TryGetValue(target, out var list) || list == null)
                {
                    list = [];
                }

                OnGetHistory?.Invoke(target, from, to, list);

                return Task.FromResult(list as IList<HistoryEntity>);
            });

        return this;
    }

    IOutSourceMoq IOutSourceMoq.Setup(IDataSource source)
    {
        return Setup(source);
    }

    public class DataSource : IDataSource
    {
        public string FilePath { get; set; } = "Data/history.json";
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped(s => new DataSource());
        services.AddScoped(s => new HistoryRepositoryMoq().Setup(s.GetRequiredService<DataSource>()));
        services.AddLazyScoped(s => s.GetRequiredService<HistoryRepositoryMoq>().Object);
    }
}
