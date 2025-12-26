using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class WalletRepositoryMoq : Mock<IWalletRepository>, IUnitLevelService
{
    public WalletRepositoryMoq() : this(FilePath)
    {
    }

    protected WalletRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        var data = JsonSerializer.Deserialize<List<WalletEntity>>(raw_text)
                       ?.GroupBy(x => x.AccountId).ToDictionary(k => k.Key, v => v.ToList())
                   ?? [];

        Setup(x => x.GetBalance(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                var balance = 0;
                if (data.TryGetValue(account_id, out var list))
                {
                    balance = list.Sum(x => x.Amount * (int)x.Direction);
                }

                return Task.FromResult(balance);
            });

        Setup(x => x.GetTransactions(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                if (!data.TryGetValue(account_id, out var list))
                {
                    list = [];
                }

                return Task.FromResult(list);
            });
    }

    private const string FilePath = "Data/Local/wallet.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<WalletRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<WalletRepositoryMoq>().Object);
    }
}