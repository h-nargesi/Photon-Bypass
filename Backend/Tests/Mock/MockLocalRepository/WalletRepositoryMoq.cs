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

        Setup(x => x.GetTransactions(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                if (!data.TryGetValue(account_id, out var list))
                {
                    list = [];
                }

                return Task.FromResult(list);
            });

        Setup(x => x.GetNotPaid(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                if (!data.TryGetValue(account_id, out var list))
                {
                    list = [];
                }

                return Task.FromResult(list);
            });

        Setup(x => x.GetInvoice(It.IsAny<string>()))
            .Returns<string>(code =>
            {
                List<WalletEntity> result;

                if (code.StartsWith('w'))
                {
                    var id = int.Parse(code[1..]);
                    result = data.Values.SelectMany(x => x.Where(r => r.Id == id))
                        .ToList();
                }
                else if (code.StartsWith('i'))
                {
                    var ic = int.Parse(code[1..]);
                    result = data.Values.SelectMany(x => x.Where(r => r.InvoiceCode == ic))
                        .ToList();
                }
                else throw new Exception($"Invalid invoice-code={code}");

                return Task.FromResult(result);
            });

        Setup(x => x.GetWalletsAmount(It.IsAny<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(ids =>
            {
                var mask_id = ids.ToHashSet();

                var result = data.Values.SelectMany(x => x.Where(r => mask_id.Contains(r.Id)))
                     .ToDictionary(k => k.Id, v => v.Amount);

                return Task.FromResult(result);
            });

        Setup(x => x.GenerateNewInvoiceCode())
            .Returns(() =>
            {
                var result = data.Values.SelectMany(x => x.Where(r => r.InvoiceCode.HasValue))
                     .Max(r => r.InvoiceCode);

                if (result == null) result = 10000;
                result += 1;

                return Task.FromResult(result);
            });

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
    }

    private const string FilePath = "Data/Local/wallet.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<WalletRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<WalletRepositoryMoq>().Object);
    }
}