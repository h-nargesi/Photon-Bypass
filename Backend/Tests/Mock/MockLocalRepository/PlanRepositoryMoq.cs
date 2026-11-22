using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

/// <summary>
/// In-memory <see cref="IPlanRepository"/> over <c>Data/Local/plan.json</c>
/// (Docs/24 fixtures: the calculators implement the closed
/// <c>Compute(PriceCalculationInput)</c> contract).
/// </summary>
public class PlanRepositoryMoq : Mock<IPlanRepository>, IUnitLevelService
{
    public readonly List<PlanEntity> Data;

    public readonly Dictionary<int, List<int>> RealmMap = new()
    {
        [1] = [1, 2, 3, 4, 5],
        [2] = [1, 2, 3, 4, 5],
        [3] = [1, 2, 3, 4, 5],
    };

    public PlanRepositoryMoq() : this(FilePath)
    {
    }

    protected PlanRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        Data = JsonSerializer.Deserialize<List<PlanEntity>>(raw_text) ?? [];

        Setup(repository => repository.Find(It.IsAny<int>()))
            .Returns<int>(id => Task.FromResult(Data.FirstOrDefault(plan => plan.Id == id)));

        Setup(repository => repository.FindByName(It.IsAny<string>()))
            .Returns<string>(name => Task.FromResult(Data.FirstOrDefault(plan => plan.Name == name)));

        Setup(repository => repository.GetActives())
            .Returns(() => Task.FromResult(Data.Where(plan => plan.IsActive).ToList()));

        Setup(repository => repository.GetActiveByGroup(It.IsAny<int>()))
            .Returns<int>(group_id => Task.FromResult(Data.Where(plan => plan.IsActive && plan.GroupId == group_id).ToList()));

        Setup(repository => repository.GetWebsitePlans(It.IsAny<int>()))
            .Returns<int>(group_id => Task.FromResult(Data
                .Where(plan => plan.IsActive && plan.ShowOnWebsite && plan.GroupId == group_id)
                .ToList()));

        Setup(repository => repository.GetAll())
            .Returns(() => Task.FromResult(Data.ToList()));

        Setup(repository => repository.GetRealmIds(It.IsAny<int>()))
            .Returns<int>(plan_id => Task.FromResult(RealmMap.GetValueOrDefault(plan_id, []).ToList()));

        Setup(repository => repository.SetRealmIds(It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
            .Returns<int, IEnumerable<int>>((plan_id, realm_ids) =>
            {
                RealmMap[plan_id] = [.. realm_ids];
                return Task.CompletedTask;
            });

        Setup(repository => repository.IsReferencedByRenewals(It.IsAny<int>()))
            .Returns<int>(_ => Task.FromResult(false));

        Setup(repository => repository.Save(It.IsAny<PlanEntity>()))
            .Returns<PlanEntity>(plan =>
            {
                var existed = Data.FirstOrDefault(p => p.Id == plan.Id);

                if (existed == null)
                {
                    plan.Id = Data.Count > 0 ? Data.Max(p => p.Id) + 1 : 1;
                    Data.Add(plan);
                }
                else
                {
                    Data.Remove(existed);
                    Data.Add(plan);
                }

                return Task.CompletedTask;
            });

        Setup(repository => repository.Save(It.IsAny<IEnumerable<PlanEntity>>()))
            .Returns<IEnumerable<PlanEntity>>(plans =>
            {
                foreach (var plan in plans)
                {
                    var existed = Data.FirstOrDefault(p => p.Id == plan.Id);

                    if (existed == null)
                    {
                        plan.Id = Data.Count > 0 ? Data.Max(p => p.Id) + 1 : 1;
                        Data.Add(plan);
                    }
                    else
                    {
                        Data.Remove(existed);
                        Data.Add(plan);
                    }
                }

                return Task.CompletedTask;
            });

        Setup(repository => repository.Delete(It.IsAny<PlanEntity>()))
            .Returns<PlanEntity>(plan =>
            {
                Data.RemoveAll(p => p.Id == plan.Id);
                return Task.CompletedTask;
            });
    }

    private const string FilePath = "Data/Local/plan.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<PlanRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<PlanRepositoryMoq>().Object);
    }
}
