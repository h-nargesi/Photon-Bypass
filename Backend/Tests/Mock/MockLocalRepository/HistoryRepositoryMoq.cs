using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class HistoryRepositoryMoq : Mock<IHistoryRepository>, IUnitLevelService
{
    public readonly List<HistoryEntity> Data = [];

    public HistoryRepositoryMoq()
    {
        Setup(x => x.GetHistory(It.IsNotNull<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns<string, DateTime?, DateTime?>((target, from, to) =>
            {
                IEnumerable<HistoryEntity> result = Data;

                if (int.TryParse(target, out var account_id))
                {
                    result = result.Where(h => h.Target == account_id);
                }

                if (from.HasValue)
                {
                    result = result.Where(h => h.Created >= from.Value);
                }

                if (to.HasValue)
                {
                    result = result.Where(h => h.Created <= to.Value);
                }

                return Task.FromResult(result.ToList());
            });

        Setup(x => x.GetLastByTitle(It.IsAny<string>()))
            .Returns<string>(title => Task.FromResult(Data
                .Where(h => h.Title == title)
                .GroupBy(h => h.Target)
                .Select(g => g.OrderByDescending(h => h.Id).First())
                .ToDictionary(h => h.Target)));

        Setup(x => x.Save(It.IsAny<HistoryEntity>()))
            .Returns<HistoryEntity>(entity =>
            {
                entity.Id = Data.Count > 0 ? Data.Max(h => h.Id) + 1 : 1;
                Data.Add(entity);

                return Task.CompletedTask;
            });

        Setup(x => x.Save(It.IsAny<string>(), It.IsAny<HistoryEntity>()))
            .Returns<string, HistoryEntity>((_, entity) =>
            {
                entity.Id = Data.Count > 0 ? Data.Max(h => h.Id) + 1 : 1;
                Data.Add(entity);

                return Task.CompletedTask;
            });
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<HistoryRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<HistoryRepositoryMoq>().Object);
    }
}
