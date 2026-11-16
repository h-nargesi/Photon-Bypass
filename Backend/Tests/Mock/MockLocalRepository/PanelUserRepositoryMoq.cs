using System.Data;
using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class PanelUserRepositoryMoq : Mock<IPanelUserRepository>, IUnitLevelService
{
    public event Action<string, PanelUserEntity?>? OnGetUserByUsername;

    public event Action<string, PanelUserEntity?>? OnGetUserByMobile;

    public event Action<string, PanelUserEntity?>? OnGetUserByEmail;

    public event Action<int, IEnumerable<PanelUserEntity>>? OnGetTargetArea;

    public event Action<PanelUserEntity>? OnSave;

    public event Action<IEnumerable<PanelUserEntity>>? OnBatchSave;

    public event Action<IEnumerable<int>>? OnSetDeactivated;

    public event Action<int, long?>? OnSetTelegramChat;

    public Dictionary<string, PanelUserEntity> Data;

    public PanelUserRepositoryMoq(TransactionalMockDbContext db_context) : this(db_context, FilePath)
    {
    }

    protected PanelUserRepositoryMoq(TransactionalMockDbContext db_context, string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        Data = JsonSerializer.Deserialize<List<PanelUserEntity>>(raw_text)
                       ?.ToDictionary(x => x.Username)
                   ?? [];

        Setup(x => x.GetActiveUser(It.IsNotNull<int>()))
            .Returns<int>(id =>
            {
                var user = Data.Values.FirstOrDefault(user => user.Id == id && user.Status != PanelUserStatus.Deactivated);
                return Task.FromResult(user);
            });

        Setup(x => x.GetUser(It.IsNotNull<string>()))
            .Returns<string>(username =>
            {
                if (!Data.TryGetValue(username, out var user))
                {
                    user = null!;
                }

                OnGetUserByUsername?.Invoke(username, user);
                return Task.FromResult(user)!;
            });

        Setup(x => x.GetActiveUsers(It.IsNotNull<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(ids =>
            {
                var mask_hash = ids.ToHashSet();
                var list = Data.Values.Where(user => mask_hash.Contains(user.Id) && user.Status != PanelUserStatus.Deactivated).ToList();
                return Task.FromResult(list.ToDictionary(user => user.Id));
            });

        Setup(x => x.GetAllActive())
            .Returns(() => Task.FromResult(Data.Values.Where(user => user.Status != PanelUserStatus.Deactivated).ToList()));

        Setup(x => x.GetUserByMobile(It.IsNotNull<string>()))
            .Returns<string>(mobile =>
            {
                var result = Data.Values.FirstOrDefault(x => x.Mobile == mobile);
                OnGetUserByMobile?.Invoke(mobile, result);
                return Task.FromResult(result)!;
            });

        Setup(x => x.GetUserByEmail(It.IsNotNull<string>()))
            .Returns<string>(email =>
            {
                var result = Data.Values.FirstOrDefault(x => x.Email == email);
                OnGetUserByEmail?.Invoke(email, result);
                return Task.FromResult(result)!;
            });

        Setup(x => x.GetUsernamesByUserId(It.IsAny<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(user_ids =>
            {
                var user_mask = user_ids.ToHashSet();
                var result = Data.Where(pair => user_mask.Contains(pair.Value.Id))
                    .ToDictionary(k => k.Value.Id, v => v.Key);
                return Task.FromResult(result);
            });

        Setup(x => x.GetActiveUserId(It.IsAny<string>()))
            .Returns<string>(username =>
            {
                if (!Data.TryGetValue(username, out var user) || user.Status == PanelUserStatus.Deactivated)
                {
                    return Task.FromResult<int?>(null);
                }

                return Task.FromResult<int?>(user.Id);
            });

        Setup(x => x.GetActiveTargetArea(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                var result = Data.Values.Where(x => x.Owner == id && x.Status != PanelUserStatus.Deactivated).ToList();
                OnGetTargetArea?.Invoke(id, result);
                return Task.FromResult(result);
            });

        Setup(x => x.CheckUniqueData(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string, string>((username, email, mobile) =>
            {
                var result = Data.Values
                    .Where(x => x.Username == username || mobile != null && x.Mobile == mobile ||
                                email != null && x.Email == email)
                    .ToList();

                var validation = (result.Any(d => d.Username == username) ? 1 : 0) |
                                 (result.Any(d => d.Email == email) ? 2 : 0) |
                                 (result.Any(d => d.Mobile == mobile) ? 4 : 0);

                return Task.FromResult(validation);
            });

        Setup(x => x.SearchUsers(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns<int?, string?, int, int>((owner_id, term, page, page_size) =>
            {
                var query = Data.Values.AsEnumerable();

                if (owner_id.HasValue)
                {
                    query = query.Where(user => user.Owner == owner_id.Value);
                }

                if (!string.IsNullOrWhiteSpace(term))
                {
                    var mask = term.Trim();

                    query = query.Where(user =>
                        user.Username.Contains(mask, StringComparison.OrdinalIgnoreCase) ||
                        (user.Name?.Contains(mask, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (user.Surname?.Contains(mask, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (user.Email?.Contains(mask, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                var ordered = query.OrderBy(user => user.Username, StringComparer.OrdinalIgnoreCase).ToList();

                var rows = ordered
                    .Skip((page - 1) * page_size)
                    .Take(page_size)
                    .Select(user => new UserOverviewModel
                    {
                        Username = user.Username,
                        Name = user.Name,
                        Surname = user.Surname,
                        Email = user.Email,
                        Role = user.Role,
                        AllowSelfService = user.UserType.HasFlag(UserTypes.AllowSelfService),
                        Status = user.Status,
                        Owner = user.Owner.HasValue
                            ? Data.Values.FirstOrDefault(owner => owner.Id == user.Owner.Value)?.Username
                            : null,
                        Created = user.Created,
                    })
                    .ToList();

                return Task.FromResult((rows, ordered.Count));
            });

        Setup(x => x.HasActiveSubordinate(It.IsAny<int>()))
            .Returns<int>(manager_id => Task.FromResult(
                Data.Values.Any(user => user.Owner == manager_id && user.Status != PanelUserStatus.Deactivated)));

        Setup(x => x.Save(It.IsAny<PanelUserEntity>()))
            .Returns<PanelUserEntity>(user =>
            {
                if (user.Id < 1)
                {
                    user.Id = Data.Count > 0 ? Data.Values.Max(existing => existing.Id) + 1 : 1;
                    Data[user.Username] = user;
                }
                else if (Data.TryGetValue(user.Username, out var existed))
                {
                    Data[user.Username] = user;
                    _ = existed;
                }
                else
                {
                    Data[user.Username] = user;
                }

                OnSave?.Invoke(user);
                return Task.CompletedTask;
            });

        Setup(x => x.Save(It.IsAny<IEnumerable<PanelUserEntity>>()))
            .Returns<IEnumerable<PanelUserEntity>>(users =>
            {
                foreach (var user in users.Where(user => user.Id < 1))
                {
                    user.Id = 10000000;
                }

                OnBatchSave?.Invoke(users);
                return Task.CompletedTask;
            });

        Setup(x => x.SetDeactivated(It.IsNotNull<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(user_ids =>
            {
                var id_mask = user_ids.ToHashSet();

                foreach (var user in Data.Values.Where(user => id_mask.Contains(user.Id)))
                {
                    user.Status = PanelUserStatus.Deactivated;
                }

                OnSetDeactivated?.Invoke(user_ids);
                return Task.CompletedTask;
            });

        Setup(x => x.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()))
            .Returns<int, long?>((user_id, chat_id) =>
            {
                var user = Data.Values.FirstOrDefault(user => user.Id == user_id);

                if (user != null)
                {
                    user.TelegramChatId = chat_id;
                }

                OnSetTelegramChat?.Invoke(user_id, chat_id);
                return Task.CompletedTask;
            });

        Setup(x => x.DbContext).Returns(db_context);
    }

    private const string FilePath = "Data/Local/panel-user.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<PanelUserRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<PanelUserRepositoryMoq>().Object);
    }
}
