using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class RenewalRepositoryMoq : Mock<IRenewalRepository>, IUnitLevelService
{
    public readonly List<RenewalEntity> Data;

    public RenewalRepositoryMoq(TransactionalMockDbContext db_context, WalletRepositoryMoq wallet_repo)
        : this(db_context, wallet_repo, FilePath)
    {
    }

    protected RenewalRepositoryMoq(TransactionalMockDbContext db_context, WalletRepositoryMoq waller_repo,
        string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        Data = JsonSerializer.Deserialize<List<RenewalEntity>>(raw_text) ?? [];

        Setup(repository => repository.GetByWalletCredit(It.IsAny<int>()))
            .Returns<int>(wallet_id => Task.FromResult(Data.FirstOrDefault(r => r.WalletCredit == wallet_id)));

        Setup(repository => repository.GetLatestByAccountIds(It.IsAny<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(account_ids =>
            {
                var mask = account_ids.ToHashSet();

                var result = Data.Where(r => mask.Contains(r.AccountId))
                    .GroupBy(r => r.AccountId)
                    .ToDictionary(group => group.Key, group => Clone(group.MaxBy(r => r.Id)!));

                return Task.FromResult(result);
            });

        Setup(repository => repository.Save(It.IsAny<RenewalEntity>()))
            .Returns<RenewalEntity>(renewal =>
            {
                var existed = Data.FirstOrDefault(r => r.Id == renewal.Id);

                if (existed == null)
                {
                    renewal.Id = Data.Count > 0 ? Data.Max(r => r.Id) + 1 : 1;
                    Data.Add(renewal);
                    db_context.RegisterUndo(() => Data.Remove(renewal));
                }
                else
                {
                    Data.Remove(existed);
                    Data.Add(renewal);
                    db_context.RegisterUndo(() =>
                    {
                        Data.Remove(renewal);
                        Data.Add(existed);
                    });
                }

                return Task.CompletedTask;
            });

        Setup(repository => repository.Save(It.IsAny<IEnumerable<RenewalEntity>>()))
            .Returns<IEnumerable<RenewalEntity>>(renewals =>
            {
                foreach (var renewal in renewals)
                {
                    var existed = Data.FirstOrDefault(r => r.Id == renewal.Id);

                    if (existed == null)
                    {
                        renewal.Id = Data.Count > 0 ? Data.Max(r => r.Id) + 1 : 1;
                        Data.Add(renewal);
                        db_context.RegisterUndo(() => Data.Remove(renewal));
                    }
                    else
                    {
                        Data.Remove(existed);
                        Data.Add(renewal);
                        db_context.RegisterUndo(() =>
                        {
                            Data.Remove(renewal);
                            Data.Add(existed);
                        });
                    }
                }

                return Task.CompletedTask;
            });

        Setup(repository => repository.DbContext).Returns(db_context);
    }

    private const string FilePath = "Data/Local/renewal.json";

    public static RenewalEntity Clone(RenewalEntity renewal)
    {
        return new RenewalEntity
        {
            Id = renewal.Id,
            AccountId = renewal.AccountId,
            RestrictedRealmId = renewal.RestrictedRealmId,
            SimultaneousUser = renewal.SimultaneousUser,
            TrafficLimit = renewal.TrafficLimit,
            TimeLimitInDays = renewal.TimeLimitInDays,
            RateLimitInMeg = renewal.RateLimitInMeg,
            WalletDebit = renewal.WalletDebit,
            WalletCredit = renewal.WalletCredit,
            Comment = renewal.Comment,
            LastSeenCounter = renewal.LastSeenCounter,
            LastPollTime = renewal.LastPollTime,
            TrafficUsed = renewal.TrafficUsed,
            LastActiveTime = renewal.LastActiveTime,
            ExpirationDate = renewal.ExpirationDate,
            CumulativeTrafficLeft = renewal.CumulativeTrafficLeft,
            Created = renewal.Created,
        };
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<RenewalRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<RenewalRepositoryMoq>().Object);
    }
}
