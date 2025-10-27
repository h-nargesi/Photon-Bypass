using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

internal class AccountRepositoryMoq : Mock<IAccountRepository>, IOutSourceMoq
{
    public event Action<string, AccountEntity?>? OnGetAccount;

    public event Action<string, AccountEntity?>? OnGetAccountByMobile;

    public event Action<string, AccountEntity?>? OnGetAccountByEmail;

    public event Action<int, IEnumerable<AccountEntity>>? OnGetTargetArea;

    public event Action<IEnumerable<int>, Dictionary<int, AccountEntity>>? OnGetAccounts;

    public AccountRepositoryMoq() : this(FilePath)
    {
    }

    protected AccountRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data = JsonSerializer.Deserialize<List<AccountEntity>>(raw_text)
                        ?.ToDictionary(x => x.Username)
                    ?? [];

        Setup(x => x.GetAccount(It.IsNotNull<string>()))
            .Returns<string>(username =>
            {
                if (!data.TryGetValue(username, out var account))
                {
                    account = null;
                }

                OnGetAccount?.Invoke(username, account);

                return Task.FromResult(account);
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
                return Task.FromResult<IList<AccountEntity>>(result);
            });

        Setup(x => x.GetAccounts(It.IsNotNull<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(user_ids =>
            {
                var result = data.Values.Where(x => user_ids.Contains(x.PermanentUserId))
                    .ToDictionary(k => k.PermanentUserId);
                OnGetAccounts?.Invoke(user_ids, result);
                return Task.FromResult<IDictionary<int, AccountEntity>>(result);
            });
    }

    private const string FilePath = "Data/account.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<AccountRepositoryMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<AccountRepositoryMoq>().Object);
    }
}