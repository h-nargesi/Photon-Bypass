using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Tools;
using System.Text.Json;

namespace PhotonBypass.Test.MockLocalRepository;

internal class AccountRepositoryMoq : Mock<IAccountRepository>, IOutSourceMoq
{
    public event Action<int, AccountEntity?>? OnGetAccountById;

    public event Action<string, AccountEntity?>? OnGetAccountByUsername;

    public event Action<string, AccountEntity?>? OnGetAccountByMobile;

    public event Action<string, AccountEntity?>? OnGetAccountByEmail;

    public event Action<int, IEnumerable<AccountEntity>>? OnGetTargetArea;

    public event Action<AccountEntity>? OnSave;

    public AccountRepositoryMoq() : this(FilePath)
    {
    }

    protected AccountRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data = JsonSerializer.Deserialize<List<AccountEntity>>(raw_text)
                       ?.ToDictionary(x => x.Username)
                   ?? [];

        Setup(x => x.GetAccount(It.IsNotNull<int>()))
            .Returns<int>(id =>
            {
                var account = data.Values.FirstOrDefault(account => account.Id == id);

                OnGetAccountById?.Invoke(id, account);
                
                return Task.FromResult(account);
            });

        Setup(x => x.GetAccount(It.IsNotNull<string>()))
            .Returns<string>(username =>
            {
                if (!data.TryGetValue(username, out var account))
                {
                    account = null;
                }

                OnGetAccountByUsername?.Invoke(username, account);

                return Task.FromResult(account);
            });

        Setup(x => x.GetAccounts(It.IsNotNull<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(ids =>
            {
                var mask_hash = ids.ToHashSet();
                var list = data.Values.Where(account => mask_hash.Contains(account.Id)).ToList();

                return Task.FromResult(list.ToDictionary(account => account.Id));
            });

        Setup(x => x.GetAccountByMobile(It.IsNotNull<string>()))
            .Returns<string>(mobile =>
            {
                var result = data.Values.FirstOrDefault(x => x.Mobile == mobile);
                OnGetAccountByMobile?.Invoke(mobile, result);
                return Task.FromResult(result);
            });

        Setup(x => x.GetAccountByEmail(It.IsNotNull<string>()))
            .Returns<string>(email =>
            {
                var result = data.Values.FirstOrDefault(x => x.Email == email);
                OnGetAccountByEmail?.Invoke(email, result);
                return Task.FromResult(result);
            });

        Setup(x => x.GetTargetArea(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                var result = data.Values.Where(x => x.Parent == id).ToList();
                OnGetTargetArea?.Invoke(id, result);
                return Task.FromResult(result);
            });

        Setup(x => x.GetActiveAccountId(It.IsAny<string>()))
            .Returns<string>(username =>
            {
                if (!data.TryGetValue(username, out var account))
                {
                    account = null;
                }

                return Task.FromResult(account?.Id);
            });

        Setup(x => x.CheckUniqueData(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string, string>((username, email, mobile) =>
            {
                var result = data.Values
                    .Where(x => x.Username == username || mobile != null && x.Mobile == mobile ||
                                email != null && x.Email == email)
                    .ToList();

                if (result.Count == 0) Task.FromResult(0);

                var validation = (result.Any(d => d.Username == username) ? 1 : 0) |
                                 (result.Any(d => d.Email == email) ? 2 : 0) |
                                 (result.Any(d => d.Mobile == mobile) ? 4 : 0);

                return Task.FromResult(validation);
            });

        Setup(x => x.Save(It.IsAny<AccountEntity>()))
            .Returns<AccountEntity>(account =>
            {
                account.Id = 10000000;
                OnSave?.Invoke(account);
                return Task.CompletedTask;
            });
    }

    private const string FilePath = "Data/account.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<AccountRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<AccountRepositoryMoq>().Object);
    }
}