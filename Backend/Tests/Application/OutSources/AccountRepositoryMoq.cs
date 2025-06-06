using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application.OutSources;

class AccountRepositoryMoq : Mock<IAccountRepository>, IOutSourceMoq
{
    Dictionary<string, AccountEntity> data = null!;

    public event Action<string, AccountEntity?>? OnGetAccount;

    public event Action<string, AccountEntity?>? OnGetAccountByMobile;

    public event Action<string, AccountEntity?>? OnGetAccountByEmail;

    public event Action<int, IEnumerable<AccountEntity>>? OnGetTargetArea;

    public event Action<IEnumerable<int>, Dictionary<int, AccountEntity>>? OnGetAccounts;

    public void Setup()
    {
        var raw_text = File.ReadAllText("Data/account.json");
        data = System.Text.Json.JsonSerializer.Deserialize<List<AccountEntity>>(raw_text)
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
                return Task.FromResult(result as IList<AccountEntity>);
            });

        Setup(x => x.GetAccounts(It.IsNotNull<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(userids =>
            {
                var result = data.Values.Where(x => userids.Contains(x.PermanentUserId))
                    .ToDictionary(k => k.PermanentUserId);
                OnGetAccounts?.Invoke(userids, result);
                return Task.FromResult(result as IDictionary<int, AccountEntity>);
            });
    }

    public static AccountRepositoryMoq CreateInstance(IServiceCollection services)
    {
        var moq = new AccountRepositoryMoq();
        moq.Setup();
        services.AddLazyScoped(s => moq.Object);
        return moq;
    }
}
