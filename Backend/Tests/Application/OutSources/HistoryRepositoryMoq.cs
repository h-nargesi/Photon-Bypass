using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application.OutSources;

class HistoryRepositoryMoq : Mock<IHistoryRepository>, IOutSourceMoq
{
    Dictionary<string, List<HistoryEntity>> data = null!;

    public event Action<string, DateTime?, DateTime?, List<HistoryEntity>>? OnGetHistory;

    public void Setup()
    {
        var raw_text = File.ReadAllText("Data/history.json");
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
    }

    public static HistoryRepositoryMoq CreateInstance(IServiceCollection services)
    {
        var moq = new HistoryRepositoryMoq();
        moq.Setup();
        services.AddLazyScoped(s => moq.Object);
        return moq;
    }
}
