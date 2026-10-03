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

    public RenewalRepositoryMoq(WalletRepositoryMoq wallet_repo) : this(wallet_repo, FilePath)
    {
    }

    protected RenewalRepositoryMoq(WalletRepositoryMoq waller_repo, string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        Data = JsonSerializer.Deserialize<List<RenewalEntity>>(raw_text) ?? [];

        Setup(repository => repository.GetNotPaid(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                var account_renewals = Data.Where(i => i.AccountId == account_id).ToList();

                List<RenewalEntity> renewals;

                if (account_renewals.Count < 1 ||
                    !waller_repo.Data.TryGetValue(account_id, out var wallets))
                {
                    renewals = [];
                }
                else
                {
                    renewals = account_renewals.Where(i => !wallets.Any(w => w.Id == i.WalletCredit && w.Status == BalanceStatus.Completed))
                        .ToList();
                }

                return Task.FromResult(renewals);
            });

        Setup(repository => repository.GetByWalletCredit(It.IsAny<int>()))
            .Returns<int>(wallet_id => Task.FromResult(Data.FirstOrDefault(r => r.WalletCredit == wallet_id)));

        Setup(repository => repository.Save(It.IsAny<RenewalEntity>()))
            .Returns<RenewalEntity>(renewal =>
            {
                var existed = Data.FirstOrDefault(r => r.Id == renewal.Id);

                if (existed == null)
                {
                    renewal.Id = Data.Count > 0 ? Data.Max(r => r.Id) + 1 : 1;
                    Data.Add(renewal);
                }
                else
                {
                    Data.Remove(existed);
                    Data.Add(renewal);
                }

                return Task.CompletedTask;
            });
    }

    private const string FilePath = "Data/Local/renewal.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<RenewalRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<RenewalRepositoryMoq>().Object);
    }
}
