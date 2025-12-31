using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;
using System.Data;
using System.Text.Json;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class WalletRepositoryMoq : Mock<IWalletRepository>, IUnitLevelService
{
    public readonly Dictionary<int, List<WalletEntity>> Data;

    public WalletRepositoryMoq(AccountRepositoryMoq account_moq) : this(account_moq, FilePath)
    {
    }

    protected WalletRepositoryMoq(AccountRepositoryMoq account_moq, string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        Data = JsonSerializer.Deserialize<List<WalletEntity>>(raw_text)
                       ?.GroupBy(x => x.AccountId).ToDictionary(k => k.Key, v => v.ToList())
                   ?? [];

        Setup(x => x.GetTransactions(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                if (!Data.TryGetValue(account_id, out var list))
                {
                    list = [];
                }

                return Task.FromResult(list);
            });

        Setup(x => x.GetNotPaid(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                if (!Data.TryGetValue(account_id, out var list))
                {
                    list = [];
                }

                list = list.Where(i => i.Status == BalanceStatus.Pending)
                    .ToList();

                return Task.FromResult(list);
            });

        Setup(x => x.GetInvoice(It.IsAny<int>()))
            .Returns<int>(code =>
            {
                var result = Data.Values.SelectMany(x => x.Where(r => r.InvoiceCode == code))
                    .ToList();

                return Task.FromResult(result);
            });

        Setup(x => x.GetInvoice(It.IsAny<string>(), It.IsAny<int>()))
            .Returns<string, int>((user, code) =>
            {
                if (!account_moq.Data.TryGetValue(user, out var account))
                {
                    return Task.FromResult(new List<WalletEntity>());
                }

                if (!Data.TryGetValue(account.Id, out var wallets))
                {
                    return Task.FromResult(new List<WalletEntity>());
                }

                var result = wallets.Where(r => r.InvoiceCode == code)
                    .ToList();

                return Task.FromResult(result);
            });

        Setup(x => x.GetWalletsAmount(It.IsAny<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(ids =>
            {
                var mask_id = ids.ToHashSet();

                var result = Data.Values.SelectMany(x => x.Where(r => mask_id.Contains(r.Id)))
                     .ToDictionary(k => k.Id, v => v.Amount);

                return Task.FromResult(result);
            });

        Setup(x => x.GenerateNewInvoiceCode())
            .Returns(() =>
            {
                var result = Data.Values.SelectMany(x => x.Where(r => r.InvoiceCode.HasValue))
                     .Max(r => r.InvoiceCode)
                     ?? 10000;

                result += 1;

                return Task.FromResult(result);
            });

        Setup(x => x.GetBalance(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                var balance = 0;
                if (Data.TryGetValue(account_id, out var list))
                {
                    balance = list.Where(w => w.Status == BalanceStatus.Completed)
                        .Sum(x => x.Amount * (int)x.Direction);
                }

                return Task.FromResult(balance);
            });

        Setup(x => x.Save(It.IsAny<WalletEntity>()))
            .Returns<WalletEntity>(wallet =>
            {
                Add(Data, wallet);

                return Task.CompletedTask;
            });

        Setup(x => x.Save(It.IsAny<IEnumerable<WalletEntity>>()))
            .Returns<IEnumerable<WalletEntity>>(wallets =>
            {
                foreach (var wallet in wallets)
                    Add(Data, wallet);

                return Task.CompletedTask;
            });

        var db_context_moq = new Mock<IDbContext>();
        db_context_moq.Setup(x => x.BeginTransactionAsync())
            .Returns(() =>
            {
                var mock = new Mock<IDbTransaction>();

                mock.Setup(t => t.Commit());
                mock.Setup(t => t.Rollback());

                return Task.FromResult(mock.Object);
            });

        Setup(x => x.DbContext).Returns(db_context_moq.Object);
    }

    private static void Add(Dictionary<int, List<WalletEntity>> data, WalletEntity wallet)
    {
        if (!data.TryGetValue(wallet.AccountId, out var list))
        {
            data[wallet.AccountId] = list = [];
        }

        if (wallet.Id < 1)
        {
            wallet.Id = list.Max(i => i.Id);
            wallet.Id++;
            list.Add(wallet);
        }
        else
        {
            var ex = list.FirstOrDefault(w => w.Id == wallet.Id);
            if (ex != null)
            {
                ex.Action = wallet.Action;
                ex.Status = wallet.Status;
                ex.Amount = wallet.Amount;
                ex.Description = wallet.Description;
                ex.Direction = wallet.Direction;
                ex.InvoiceCode = wallet.InvoiceCode;
                ex.ReferenceCode = wallet.ReferenceCode;
                ex.RenewId = wallet.RenewId;
            }
        }
    }

    private const string FilePath = "Data/Local/wallet.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<WalletRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<WalletRepositoryMoq>().Object);
    }
}