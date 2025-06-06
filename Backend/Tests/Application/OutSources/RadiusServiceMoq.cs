using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application.OutSources;

class RadiusServiceMoq : Mock<IRadiusService>, IOutSourceMoq
{
    public PermanentUsersRepositoryMoq? PermanentUsersRepoMoq { get; set; }

    public UserPlanStateRepositoryMoq? UserPlanStateRepoMoq { get; set; }

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

    public void Setup()
    {
        var raw_text = File.ReadAllText("Data/passwords.json");
        passwords = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(raw_text)
            ?? [];

        raw_text = File.ReadAllText("Data/traffic-data-radius.json");
        traffic_data = System.Text.Json.JsonSerializer.Deserialize<TrafficDataRadius[]>(raw_text)
            ?? [];

        Setup(x => x.ActivePermanentUser(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns<int, bool>((user_id, active) =>
            {
                if (PermanentUsersRepoMoq == null) throw new ArgumentNullException(nameof(PermanentUsersRepoMoq));

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
                if (PermanentUsersRepoMoq == null) throw new ArgumentNullException(nameof(PermanentUsersRepoMoq));

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
                if (PermanentUsersRepoMoq == null) throw new ArgumentNullException(nameof(PermanentUsersRepoMoq));

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
                if (PermanentUsersRepoMoq == null) throw new ArgumentNullException(nameof(PermanentUsersRepoMoq));

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
                if (PermanentUsersRepoMoq == null) throw new ArgumentNullException(nameof(PermanentUsersRepoMoq));

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
                if (PermanentUsersRepoMoq == null) throw new ArgumentNullException(nameof(PermanentUsersRepoMoq));

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
                if (PermanentUsersRepoMoq == null) throw new ArgumentNullException(nameof(PermanentUsersRepoMoq));

                OnFetchTrafficData?.Invoke(user, index, type, traffic_data);

                return Task.FromResult(traffic_data);
            });

        Setup(x => x.SetRestrictedServer(It.IsNotNull<string>(), It.IsAny<string>()))
            .Returns<string, string?>((username, server_ip) =>
            {
                if (UserPlanStateRepoMoq == null) throw new ArgumentNullException(nameof(UserPlanStateRepoMoq));

                var current = UserPlanStateRepoMoq.Data.Values.FirstOrDefault(x => x.Username == username);
                if (current != null)
                {
                    current.RestrictedServerIP = server_ip;
                }

                OnSetRestrictedServer?.Invoke(username, server_ip, current != null);

                return Task.FromResult(current != null);
            });
    }

    public static RadiusServiceMoq CreateInstance(IServiceCollection services)
    {
        var moq = new RadiusServiceMoq();
        moq.Setup();
        services.AddLazyScoped(s => moq.Object);
        return moq;
    }
}
