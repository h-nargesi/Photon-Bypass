using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Test.MockOutSources.Models;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

class RadiusServiceMoq(IServiceProvider service) : Mock<IRadiusService>, IOutSourceMoq
{
    IServiceProvider ServiceProvider { get; } = service;

    Dictionary<int, string> passwords = null!;

    TrafficDataRadius[] traffic_data = null!;

    public event Action<int, bool, bool>? OnActivePermanentUser;

    public event Action<int, string?>? OnGetOvpnPassword;

    public event Action<int, string, bool>? OnChangeOvpnPassword;

    public event Action<PermanentUserEntity, bool>? OnSaveUserBaiscInfo;

    public event Action<PermanentUserEntity, bool>? OnSaveUserPersonalInfo;

    public event Action<PermanentUserEntity, string, bool>? OnRegisterPermenentUser;

    public event Action<string, DateTime, TrafficDataRequestType, TrafficDataRadius[]>? OnFetchTrafficData;

    public event Action<string, string?, bool>? OnSetRestrictedServer;

    public event Action<string, long, bool>? OnUpdateUserDataUsege;

    public event Action<int, PlanType, int, string, bool>? OnInsertTopUpAndMakeActive;

    public RadiusServiceMoq Setup(IDataSource source)
    {
        if (source is not DataSource data_srouce) throw new ArgumentException(null, nameof(source));

        var raw_text = File.ReadAllText(data_srouce.Passwords);
        passwords = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(raw_text)
            ?? [];

        raw_text = File.ReadAllText(data_srouce.FilePath);
        traffic_data = System.Text.Json.JsonSerializer.Deserialize<TrafficDataRadiusMoqModel[]>(raw_text)
            ?.Select(x => x.ToEntity())
            .ToArray()
            ?? [];

        Setup(x => x.ActivePermanentUser(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns<int, bool>((user_id, active) =>
            {
                var PermanentUsersRepoMoq = ServiceProvider.GetRequiredService<PermanentUsersRepositoryMoq>();

                bool result;
                var user = PermanentUsersRepoMoq.Data.Values.FirstOrDefault(x => x.Id == user_id);
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
                var PermanentUsersRepoMoq = ServiceProvider.GetRequiredService<PermanentUsersRepositoryMoq>();

                string? result;
                var user = PermanentUsersRepoMoq.Data.Values.FirstOrDefault(x => x.Id == user_id);
                if (user != null && passwords.TryGetValue(user.Id, out var password))
                {
                    result = password;
                }
                else result = null;

                OnGetOvpnPassword?.Invoke(user_id, result);

                return Task.FromResult(result);
            });

        Setup(x => x.ChangeOvpnPassword(It.IsAny<int>(), It.IsNotNull<string>()))
            .Returns<int, string>((user_id, password) =>
            {
                var PermanentUsersRepoMoq = ServiceProvider.GetRequiredService<PermanentUsersRepositoryMoq>();

                var result = PermanentUsersRepoMoq.Data.Values.Any(x => x.Id == user_id);
                if (result)
                {
                    passwords[user_id] = password;
                }

                OnChangeOvpnPassword?.Invoke(user_id, password, result);

                return Task.FromResult(result);
            });

        Setup(x => x.SaveUserBaiscInfo(It.IsNotNull<PermanentUserEntity>()))
            .Returns<PermanentUserEntity>(user =>
            {
                var PermanentUsersRepoMoq = ServiceProvider.GetRequiredService<PermanentUsersRepositoryMoq>();

                var result = PermanentUsersRepoMoq.Data.TryGetValue(user.Username, out var current);
                if (current != null)
                {
                    current.FromDate = user.FromDate;
                    current.ToDate = user.ToDate;
                    current.Profile = user.Profile;
                    current.ProfileId = user.ProfileId;
                    current.Realm = user.Realm;
                    current.RealmId = user.RealmId;
                }

                OnSaveUserBaiscInfo?.Invoke(user, result);

                return Task.FromResult(result);
            });

        Setup(x => x.SaveUserPersonalInfo(It.IsNotNull<PermanentUserEntity>()))
            .Returns<PermanentUserEntity>(user =>
            {
                var PermanentUsersRepoMoq = ServiceProvider.GetRequiredService<PermanentUsersRepositoryMoq>();

                var result = PermanentUsersRepoMoq.Data.TryGetValue(user.Username, out var current);
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
                var PermanentUsersRepoMoq = ServiceProvider.GetRequiredService<PermanentUsersRepositoryMoq>();

                var result = !PermanentUsersRepoMoq.Data.ContainsKey(user.Username);
                if (result)
                {
                    user.Id = 1 + PermanentUsersRepoMoq.Data.Values.Max(x => x.Id);
                    user.Username += "@web";
                    passwords[user.Id] = password;
                    PermanentUsersRepoMoq.Data.Add(user.Username, user);
                }

                OnRegisterPermenentUser?.Invoke(user, password, result);

                return Task.FromResult(result);
            });

        Setup(x => x.FetchTrafficData(
            It.IsNotNull<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<TrafficDataRequestType>()))
            .Returns<string, DateTime, TrafficDataRequestType>((user, index, type) =>
            {
                var PermanentUsersRepoMoq = ServiceProvider.GetRequiredService<PermanentUsersRepositoryMoq>();

                OnFetchTrafficData?.Invoke(user, index, type, traffic_data);

                return Task.FromResult(traffic_data);
            });

        Setup(x => x.SetRestrictedServer(It.IsNotNull<string>(), It.IsAny<string>()))
            .Returns<string, string?>((username, server_ip) =>
            {
                var UserPlanStateRepoMoq = ServiceProvider.GetRequiredService<UserPlanStateRepositoryMoq>();

                var current = UserPlanStateRepoMoq.Data.Values.FirstOrDefault(x => x.Username == username);
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
                OnUpdateUserDataUsege?.Invoke(username, total_data, true);
                return Task.FromResult(true);
            });

        Setup(x => x.InsertTopUpAndMakeActive(It.IsAny<int>(), It.IsAny<PlanType>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns<int, PlanType, int, string>((user_id, type, value, comment) =>
            {
                OnInsertTopUpAndMakeActive?.Invoke(user_id, type, value, comment, true);
                return Task.FromResult(true);
            });

        return this;
    }

    IOutSourceMoq IOutSourceMoq.Setup(IDataSource source)
    {
        return Setup(source);
    }

    public class DataSource : IDataSource
    {
        public string FilePath { get; set; } = "Data/traffic-data-radius.json";

        public string Passwords { get; set; } = "Data/passwords.json";
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped(s => new DataSource());
        services.AddScoped(s => new RadiusServiceMoq(s).Setup(s.GetRequiredService<DataSource>()));
        services.AddLazyScoped(s => s.GetRequiredService<RadiusServiceMoq>().Object);
    }
}
