using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Test.MockOutSources.Models;
using PhotonBypass.Tools;
using System.Text.Json;

namespace PhotonBypass.Test.MockOutSources;

internal class RadiusServiceMoq : Mock<IRadiusService>, IOutSourceMoq
{
    public event Action<int, bool, bool>? OnActivePermanentUser;

    public event Action<int, string?>? OnGetOVpnPassword;

    public event Action<int, string, bool>? OnChangeOVpnPassword;

    public event Action<PermanentUserEntity, bool>? OnSaveUserBasicInfo;

    public event Action<PermanentUserEntity, bool>? OnSaveUserPersonalInfo;

    public event Action<PermanentUserEntity, string, bool>? OnRegisterPermanentUser;

    public event Action<string, DateTime, TrafficDataRequestType, TrafficDataRadius[]>? OnFetchTrafficData;

    public event Action<string, string?, bool>? OnSetRestrictedServer;

    public event Action<string, long, bool>? OnUpdateUserDataUsage;

    public event Action<int, PlanType, int, string, bool>? OnInsertTopUpAndMakeActive;

    public RadiusServiceMoq(IServiceProvider service) : this(service, FilePath, Passwords)
    {
    }

    protected RadiusServiceMoq(IServiceProvider service, string file_path, string passwords_path)
    {
        var raw_text = File.ReadAllText(passwords_path);
        var passwords1 = JsonSerializer.Deserialize<Dictionary<string, string>>(raw_text)?
                             .Select(x => new { Key = int.Parse(x.Key), x.Value })
                             .ToDictionary(k => k.Key, v => v.Value)
                         ?? [];

        raw_text = File.ReadAllText(file_path);
        var traffic_data = JsonSerializer.Deserialize<TrafficDataRadiusMoqModel[]>(raw_text)
                               ?.Select(x => x.ToEntity())
                               .ToArray()
                           ?? [];

        Setup(x => x.ActivePermanentUser(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns<int, bool>((user_id, active) =>
            {
                var permanent_users_repo_moq = service.GetRequiredService<PermanentUsersRepositoryMoq>();

                bool result;
                var user = permanent_users_repo_moq.Data.Values.FirstOrDefault(x => x.Id == user_id);
                if (user != null)
                {
                    user.Active = active;
                    result = true;
                }
                else result = false;

                OnActivePermanentUser?.Invoke(user_id, active, result);

                return Task.FromResult(result);
            });

        Setup(x => x.GetOvpnPassword(It.IsAny<int>()))
            .Returns<int>(user_id =>
            {
                var permanent_users_repo_moq = service.GetRequiredService<PermanentUsersRepositoryMoq>();

                string? result;
                var user = permanent_users_repo_moq.Data.Values.FirstOrDefault(x => x.Id == user_id);
                if (user != null && passwords1.TryGetValue(user.Id, out var password))
                {
                    result = password;
                }
                else result = null;

                OnGetOVpnPassword?.Invoke(user_id, result);

                return Task.FromResult(result);
            });

        Setup(x => x.ChangeOvpnPassword(It.IsAny<int>(), It.IsNotNull<string>()))
            .Returns<int, string>((user_id, password) =>
            {
                var permanent_users_repo_moq = service.GetRequiredService<PermanentUsersRepositoryMoq>();

                var result = permanent_users_repo_moq.Data.Values.Any(x => x.Id == user_id);
                if (result)
                {
                    passwords1[user_id] = password;
                }

                OnChangeOVpnPassword?.Invoke(user_id, password, result);

                return Task.FromResult(result);
            });

        Setup(x => x.SaveUserBaiscInfo(It.IsNotNull<PermanentUserEntity>()))
            .Returns<PermanentUserEntity>(user =>
            {
                var permanent_users_repo_moq = service.GetRequiredService<PermanentUsersRepositoryMoq>();

                var result = permanent_users_repo_moq.Data.TryGetValue(user.Username, out var current);
                if (current != null)
                {
                    current.FromDate = user.FromDate;
                    current.ToDate = user.ToDate;
                    current.Profile = user.Profile;
                    current.ProfileId = user.ProfileId;
                    current.Realm = user.Realm;
                    current.RealmId = user.RealmId;
                }

                OnSaveUserBasicInfo?.Invoke(user, result);

                return Task.FromResult(result);
            });

        Setup(x => x.SaveUserPersonalInfo(It.IsNotNull<PermanentUserEntity>()))
            .Returns<PermanentUserEntity>(user =>
            {
                var permanent_users_repo_moq = service.GetRequiredService<PermanentUsersRepositoryMoq>();

                var result = permanent_users_repo_moq.Data.TryGetValue(user.Username, out var current);
                if (current != null)
                {
                    current.Name = user.Name;
                    current.Surname = user.Surname;
                    current.Phone = user.Phone;
                    current.Email = user.Email;
                }

                OnSaveUserPersonalInfo?.Invoke(user, result);

                return Task.FromResult(result);
            });

        Setup(x => x.RegisterPermenentUser(It.IsNotNull<PermanentUserEntity>(), It.IsNotNull<string>()))
            .Returns<PermanentUserEntity, string>((user, password) =>
            {
                var permanent_users_repo_moq = service.GetRequiredService<PermanentUsersRepositoryMoq>();

                var result = !permanent_users_repo_moq.Data.ContainsKey(user.Username);
                if (result)
                {
                    user.Id = 1 + permanent_users_repo_moq.Data.Values.Max(x => x.Id);
                    user.Username += "@web";
                    passwords1[user.Id] = password;
                    permanent_users_repo_moq.Data.Add(user.Username, user);
                }

                OnRegisterPermanentUser?.Invoke(user, password, result);

                return Task.FromResult(result);
            });

        Setup(x => x.FetchTrafficData(
                It.IsNotNull<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<TrafficDataRequestType>()))
            .Returns<string, DateTime, TrafficDataRequestType>((user, index, type) =>
            {
                //var PermanentUsersRepoMoq = service.GetRequiredService<PermanentUsersRepositoryMoq>();

                OnFetchTrafficData?.Invoke(user, index, type, traffic_data);

                return Task.FromResult(traffic_data);
            });

        Setup(x => x.SetRestrictedServer(It.IsNotNull<string>(), It.IsAny<string>()))
            .Returns<string, string?>((username, server_ip) =>
            {
                var user_plan_state_repo_moq = service.GetRequiredService<UserPlanStateRepositoryMoq>();

                var current = user_plan_state_repo_moq.Data.Values.FirstOrDefault(x => x.Username == username);
                if (current != null)
                {
                    current.RestrictedServerIP = server_ip;
                }

                OnSetRestrictedServer?.Invoke(username, server_ip, current != null);

                return Task.FromResult(current != null);
            });

        Setup(x => x.UpdateUserDataUsege(It.IsNotNull<string>(), It.IsAny<int>()))
            .Returns<string, int>((username, total_data) =>
            {
                OnUpdateUserDataUsage?.Invoke(username, total_data, true);
                return Task.FromResult(true);
            });

        Setup(x => x.InsertTopUpAndMakeActive(It.IsAny<int>(), It.IsAny<PlanType>(), It.IsAny<int>(),
                It.IsAny<string>()))
            .Returns<int, PlanType, int, string>((user_id, type, value, comment) =>
            {
                OnInsertTopUpAndMakeActive?.Invoke(user_id, type, value, comment, true);
                return Task.FromResult(true);
            });
    }

    private const string FilePath = "Data/traffic-data-radius.json";

    private const string Passwords = "Data/passwords.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<RadiusServiceMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<RadiusServiceMoq>().Object);
    }
}