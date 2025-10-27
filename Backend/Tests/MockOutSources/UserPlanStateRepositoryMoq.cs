using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

internal class UserPlanStateRepositoryMoq : Mock<IUserPlanStateRepository>, IOutSourceMoq
{
    public Dictionary<int, UserPlanStateEntity> Data { get; }

    public event Action<int, string?>? OnGetRestrictedServerIp;

    public event Action<int, UserPlanStateEntity?>? OnGetPlanState;

    public event Action<string, UserPlanStateEntity?>? OnGetPlanStateByUsername;

    public event Action<float, List<UserPlanStateEntity>>? OnGetPlanOverState;

    public UserPlanStateRepositoryMoq() : this(FilePath)
    {
    }

    protected UserPlanStateRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        Data = JsonSerializer.Deserialize<List<UserPlanStateEntity>>(raw_text)
                   ?.ToDictionary(x => x.Id)
               ?? [];

        Setup(x => x.GetRestrictedServerIP(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                if (!Data.TryGetValue(id, out var user_plan))
                {
                    user_plan = null;
                }

                OnGetRestrictedServerIp?.Invoke(id, user_plan?.RestrictedServerIP);

                return Task.FromResult(user_plan?.RestrictedServerIP);
            });

        Setup(x => x.GetPlanState(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                if (!Data.TryGetValue(id, out var userplan))
                {
                    userplan = null;
                }

                OnGetPlanState?.Invoke(id, userplan);

                return Task.FromResult(userplan);
            });

        Setup(x => x.GetPlanState(It.IsNotNull<string>()))
            .Returns<string>(username =>
            {
                var result = Data.Values.FirstOrDefault(x => x.Username == username);
                OnGetPlanStateByUsername?.Invoke(username, result);
                return Task.FromResult(result);
            });

        Setup(x => x.GetPlanOverState(It.IsAny<float>()))
            .Returns<float>(percent =>
            {
                var result = Data.Values
                    .Where(x => x.LeftDays.HasValue && x.LeftDays > percent * 30 ||
                                x.GigaLeft.HasValue && x.GigaLeft > percent * 50)
                    .ToList();
                OnGetPlanOverState?.Invoke(percent, result);
                return Task.FromResult(result as IList<UserPlanStateEntity>);
            });
    }

    private const string FilePath = "Data/user-plan-state.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<UserPlanStateRepositoryMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<UserPlanStateRepositoryMoq>().Object);
    }
}