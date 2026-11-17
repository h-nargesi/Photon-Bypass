using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class VpnAccountRepositoryMoq : Mock<IVpnAccountRepository>, IUnitLevelService
{
    public event Action<int, VpnAccountEntity?>? OnGetAccountById;

    public event Action<string, VpnAccountEntity?>? OnGetAccountByUsername;

    public event Action<VpnAccountEntity>? OnSave;

    public event Action<IEnumerable<VpnAccountEntity>>? OnBatchSave;

    public event Action<IEnumerable<int>>? OnUpdateWarningTimes;

    public event Action<IEnumerable<int>, VpnAccountStatus>? OnSetStatus;

    public List<VpnAccountEntity> Data;

    private readonly PanelUserRepositoryMoq panel_user_moq;

    public VpnAccountRepositoryMoq(TransactionalMockDbContext db_context, PanelUserRepositoryMoq panel_user_moq) : this(db_context, panel_user_moq, FilePath)
    {
    }

    protected VpnAccountRepositoryMoq(TransactionalMockDbContext db_context, PanelUserRepositoryMoq panel_user_moq, string file_path)
    {
        this.panel_user_moq = panel_user_moq;
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        Data = JsonSerializer.Deserialize<List<VpnAccountEntity>>(raw_text) ?? [];

        Setup(x => x.GetAccount(It.IsNotNull<int>()))
            .Returns<int>(id =>
            {
                var account = Data.FirstOrDefault(account => account.Id == id);
                OnGetAccountById?.Invoke(id, account);
                return Task.FromResult(account)!;
            });

        Setup(x => x.GetVisibleAccount(It.IsNotNull<int>()))
            .Returns<int>(id =>
            {
                var account = Data.FirstOrDefault(account => account.Id == id && account.Status != VpnAccountStatus.Deactivated);
                return Task.FromResult(account)!;
            });

        Setup(x => x.GetAccount(It.IsNotNull<string>()))
            .Returns<string>(username =>
            {
                var account = Data.FirstOrDefault(account => account.Username == username);
                OnGetAccountByUsername?.Invoke(username, account);
                return Task.FromResult(account)!;
            });

        Setup(x => x.GetByUser(It.IsAny<int>()))
            .Returns<int>(user_id => Task.FromResult(Data.Where(account => account.UserId == user_id).ToList()));

        Setup(x => x.GetVisibleByUser(It.IsAny<int>()))
            .Returns<int>(user_id => Task.FromResult(Data.Where(account => account.UserId == user_id && account.Status != VpnAccountStatus.Deactivated).ToList()));

        Setup(x => x.GetVisibleByUsers(It.IsAny<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(user_ids =>
            {
                var mask = user_ids.ToHashSet();
                return Task.FromResult(Data.Where(account => mask.Contains(account.UserId) && account.Status != VpnAccountStatus.Deactivated).ToList());
            });

        Setup(x => x.GetAll())
            .Returns(() => Task.FromResult(Data.ToList()));

        Setup(x => x.GetAccountIdByUsername(It.IsNotNull<HashSet<string>>()))
            .Returns<HashSet<string>>(names =>
            {
                var result = Data.Where(account => names.Contains(account.Username))
                    .ToDictionary(k => k.Username, v => v.Id);
                return Task.FromResult(result);
            });

        Setup(x => x.GetUsernamesByAccountId(It.IsAny<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(account_ids =>
            {
                var account_mask = account_ids.ToHashSet();
                var result = Data.Where(account => account_mask.Contains(account.Id))
                    .ToDictionary(k => k.Id, v => v.Username);
                return Task.FromResult(result);
            });

        Setup(x => x.GetActiveAccounts(It.IsNotNull<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(ids =>
            {
                var mask_hash = ids.ToHashSet();
                var list = Data.Where(account => mask_hash.Contains(account.Id) && account.Status == VpnAccountStatus.Active).ToList();
                return Task.FromResult(list.ToDictionary(account => account.Id));
            });

        Setup(x => x.GetAccountsOverview(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(usernames =>
            {
                var mask = usernames.ToHashSet();
                var rows = Data.Where(account => panel_user_moq.Data.Values
                        .Any(user => user.Id == account.UserId && mask.Contains(user.Username)))
                    .Select(account => new UserAccountOverviewModel
                    {
                        Owner = panel_user_moq.Data.Values.First(user => user.Id == account.UserId).Username,
                        UserId = account.UserId,
                        AccountId = account.Id,
                        Username = account.Username,
                        RealmId = account.RealmId,
                        Status = account.Status,
                        Created = account.Created,
                    })
                    .ToList();
                return Task.FromResult(rows);
            });

        Setup(x => x.SuggestUsername(It.IsNotNull<string>()))
            .Returns<string>(panel_username =>
            {
                var taken = Data.Select(account => account.Username).ToHashSet();

                foreach (var candidate in PhotonBypass.Domain.Account.Business.AccountBusiness.SuggestVpnUsernameSequence(panel_username))
                {
                    if (!taken.Contains(candidate))
                    {
                        return Task.FromResult(candidate);
                    }
                }

                throw new PhotonBypass.ErrorHandler.UserException("ظرفیت نام کاربری برای این کاربر تکمیل است!",
                    $"No free VPN username suggestion within the length cap (Docs/22 D3): owner={panel_username}");
            });

        Setup(x => x.Save(It.IsAny<VpnAccountEntity>()))
            .Returns<VpnAccountEntity>(account =>
            {
                if (account.Id < 1)
                {
                    account.Id = 10000000 + Data.Count;
                    Data.Add(account);
                }
                else
                {
                    var index = Data.FindIndex(existing => existing.Id == account.Id);

                    if (index >= 0)
                    {
                        Data[index] = account;
                    }
                    else
                    {
                        Data.Add(account);
                    }
                }

                OnSave?.Invoke(account);
                return Task.CompletedTask;
            });

        Setup(x => x.Save(It.IsAny<IEnumerable<VpnAccountEntity>>()))
            .Returns<IEnumerable<VpnAccountEntity>>(accounts =>
            {
                foreach (var account in accounts)
                {
                    if (account.Id < 1)
                    {
                        account.Id = 10000000 + Data.Count;
                        Data.Add(account);
                    }
                }

                OnBatchSave?.Invoke(accounts);
                return Task.CompletedTask;
            });

        Setup(x => x.UpdateWarningTimes(It.IsNotNull<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(account_ids =>
            {
                var id_mask = account_ids.ToHashSet();

                foreach (var account in Data.Where(account => id_mask.Contains(account.Id)))
                {
                    account.LastWarningTime = DateTime.Now;
                }

                OnUpdateWarningTimes?.Invoke(account_ids);
                return Task.CompletedTask;
            });

        Setup(x => x.SetStatus(It.IsNotNull<IEnumerable<int>>(), It.IsAny<VpnAccountStatus>()))
            .Returns<IEnumerable<int>, VpnAccountStatus>((account_ids, status) =>
            {
                var id_mask = account_ids.ToHashSet();

                foreach (var account in Data.Where(account => id_mask.Contains(account.Id)))
                {
                    account.Status = status;
                }

                OnSetStatus?.Invoke(account_ids, status);
                return Task.CompletedTask;
            });

        Setup(x => x.DbContext).Returns(db_context);
    }

    private const string FilePath = "Data/Local/vpn-account.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<VpnAccountRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<VpnAccountRepositoryMoq>().Object);
    }
}
