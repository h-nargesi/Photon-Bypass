using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Tools;
using System.Text.Json;

namespace PhotonBypass.Test.MockLocalRepository;

internal class HistoryRepositoryMoq : Mock<IHistoryRepository>, IOutSourceMoq
{
    public event Action<string, DateTime?, DateTime?, List<HistoryEntity>>? OnGetHistory;

    public HistoryRepositoryMoq() : this(FilePath)
    {
    }

    protected HistoryRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data_dictionary = JsonSerializer.Deserialize<List<HistoryEntity>>(raw_text)
                        ?.GroupBy(k => k.Target)
                        .ToDictionary(x => x.Key, v => v.ToList())
                    ?? [];

        Setup(x => x.GetHistory(It.IsNotNull<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns<string, DateTime?, DateTime?>((target, from, to) =>
            {
                if (!data_dictionary.TryGetValue(target, out var list))
                {
                    list = [];
                }

                OnGetHistory?.Invoke(target, from, to, list);

                return Task.FromResult<IList<HistoryEntity>>(list);
            });
    }

    private const string FilePath = "Data/history.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<HistoryRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<HistoryRepositoryMoq>().Object);
    }
}