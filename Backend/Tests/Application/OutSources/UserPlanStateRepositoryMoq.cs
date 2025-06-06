using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace PhotonBypass.Test.Application.OutSources;

class UserPlanStateRepositoryMoq : Mock<IUserPlanStateRepository>, IOutSourceMoq
{
    public Dictionary<int, UserPlanStateEntity> Data { get; private set; } = null!;

    public event Action<int, string?>? OnGetRestrictedServerIP;

    public event Action<int, UserPlanStateEntity?>? OnGetPlanState;

    public event Action<string, UserPlanStateEntity?>? OnGetPlanStateByUsername;

    public event Action<float, List<UserPlanStateEntity>>? OnGetPlanOverState;

    public void Setup()
    {
        var raw_text = File.ReadAllText("Data/user-plan-state.json");
        Data = System.Text.Json.JsonSerializer.Deserialize<List<UserPlanStateEntity>>(raw_text)
            ?.ToDictionary(x => x.Id)
            ?? [];

        Setup(x => x.GetRestrictedServerIP(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                if (!Data.TryGetValue(id, out var userplan))
                {
                    userplan = null;
                }

                OnGetRestrictedServerIP?.Invoke(id, userplan?.RestrictedServerIP);

                return Task.FromResult(userplan?.RestrictedServerIP);
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
                var result = Data.Values.Where(x => (x.LeftDays.HasValue && x.LeftDays > percent * 30) || (x.GigaLeft.HasValue && x.GigaLeft > percent * 50))
                    .ToList();
                OnGetPlanOverState?.Invoke(percent, result);
                return Task.FromResult(result as IList<UserPlanStateEntity>);
            });
    }

    public static UserPlanStateRepositoryMoq CreateInstance(IServiceCollection services)
    {
        var moq = new UserPlanStateRepositoryMoq();
        moq.Setup();
        services.AddLazyScoped(s => moq.Object);
        return moq;
    }
}