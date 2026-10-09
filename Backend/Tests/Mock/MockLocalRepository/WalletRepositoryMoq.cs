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

    public WalletRepositoryMoq(TransactionalMockDbContext db_context, AccountRepositoryMoq account_moq)
        : this(db_context, account_moq, FilePath)
    {
    }

    protected WalletRepositoryMoq(TransactionalMockDbContext db_context, AccountRepositoryMoq account_moq,
        string file_path)
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

        Setup(x => x.GetAccountIdsBelowThreshold(It.IsAny<int>()))
            .Returns<int>(threshold =>
            {
                var result = Data
                    .Where(p => p.Value
                        .Where(w => w.Status is BalanceStatus.Completed or BalanceStatus.Verifying)
                        .Sum(w => w.Amount * (int)w.Direction) < threshold)
                    .Select(p => p.Key)
                    .ToList();

                return Task.FromResult(result);
            });

        Setup(x => x.GetBalance(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                var balance = 0;
                if (Data.TryGetValue(account_id, out var list))
                {
                    balance = list.Where(w => w.Status is BalanceStatus.Completed or BalanceStatus.Verifying)
                        .Sum(x => x.Amount * (int)x.Direction);
                }

                return Task.FromResult(balance);
            });

        Setup(x => x.Save(It.IsAny<WalletEntity>()))
            .Returns<WalletEntity>(wallet =>
            {
                Add(db_context, Data, wallet);

                return Task.CompletedTask;
            });

        Setup(x => x.Save(It.IsAny<IEnumerable<WalletEntity>>()))
            .Returns<IEnumerable<WalletEntity>>(wallets =>
            {
                foreach (var wallet in wallets)
                    Add(db_context, Data, wallet);

                return Task.CompletedTask;
            });

        Setup(x => x.DbContext).Returns(db_context);
    }

    private static void Add(TransactionalMockDbContext db_context, Dictionary<int, List<WalletEntity>> data,
        WalletEntity wallet)
    {
        if (!data.TryGetValue(wallet.AccountId, out var list))
        {
            data[wallet.AccountId] = list = [];
        }

        if (wallet.Id < 1)
        {
            wallet.Id = data.Values.SelectMany(x => x).Select(i => i.Id).DefaultIfEmpty(0).Max() + 1;
            list.Add(wallet);
            db_context.RegisterUndo(() => list.Remove(wallet));
        }
        else
        {
            var ex = list.FirstOrDefault(w => w.Id == wallet.Id);
            if (ex != null)
            {
                var snapshot = new WalletEntity
                {
                    Id = ex.Id,
                    AccountId = ex.AccountId,
                    Action = ex.Action,
                    Status = ex.Status,
                    Amount = ex.Amount,
                    Description = ex.Description,
                    Direction = ex.Direction,
                    InvoiceCode = ex.InvoiceCode,
                    ReferenceCode = ex.ReferenceCode,
                    RenewId = ex.RenewId,
                };
                db_context.RegisterUndo(() =>
                {
                    ex.Action = snapshot.Action;
                    ex.Status = snapshot.Status;
                    ex.Amount = snapshot.Amount;
                    ex.Description = snapshot.Description;
                    ex.Direction = snapshot.Direction;
                    ex.InvoiceCode = snapshot.InvoiceCode;
                    ex.ReferenceCode = snapshot.ReferenceCode;
                    ex.RenewId = snapshot.RenewId;
                });

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
