using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockLocalRepository;

public class PlanStateRepositoryMoq : Mock<IPlanStateRepository>, IOutSourceMoq
{
    public PlanStateRepositoryMoq() : this(FilePath)
    {
    }

    protected PlanStateRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data_dictionary = JsonSerializer.Deserialize<List<PlanStateEntity>>(raw_text)
                       ?.ToDictionary(x => x.Id)
                   ?? [];

        Setup(x => x.GetAll())
            .Returns(() => Task.FromResult(data_dictionary.Values.ToList()));
        
        Setup(x => x.GetPlanState(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                if (!data_dictionary.TryGetValue(id, out var state))
                {
                    state = null;
                }

                return Task.FromResult(state);
            });
        
        Setup(x => x.GetActiveAccountRealmId(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                if (!data_dictionary.TryGetValue(id, out var state) || !state.Active)
                {
                    return Task.FromResult<(int, int?)?>(null);
                }

                return Task.FromResult<(int, int?)?>((id, state.RestrictedRealmId));
            });
    }

    private const string FilePath = "Data/plan-state.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<PlanStateRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<PlanStateRepositoryMoq>().Object);
    }
}