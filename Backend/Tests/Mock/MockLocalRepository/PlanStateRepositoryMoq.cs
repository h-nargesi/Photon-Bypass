using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Test.Mock.MockRadiusDesk.Model;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

public class PlanStateRepositoryMoq : Mock<IPlanStateRepository>, IUnitLevelService
{
    public PlanStateRepositoryMoq() : this(FilePath)
    {
    }

    protected PlanStateRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        var data_dictionary = JsonSerializer.Deserialize<List<PlanStateMoqModel>>(raw_text)
                                  ?.Select(m => m.ToEntity())
                                  .ToDictionary(x => x.Id)
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

    }


    private const string FilePath = "Data/Local/plan-state.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<PlanStateRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<PlanStateRepositoryMoq>().Object);
    }
}