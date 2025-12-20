using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class RenewalRepositoryMoq : Mock<IRenewalRepository>, IUnitLevelService
{
    public RenewalRepositoryMoq() : this(FilePath)
    {
    }

    protected RenewalRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        var data_dictionary = JsonSerializer.Deserialize<List<RenewalEntity>>(raw_text)
            ?.ToDictionary(k => k.AccountId) ?? [];

        Setup(repository => repository.GetTopRestrictedRealmId(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                if (data_dictionary.TryGetValue(account_id, out var renewal))
                {
                    renewal = null;
                }

                return Task.FromResult(renewal?.RestrictedRealmId);
            });
    }

    private const string FilePath = "Data/Local/renewal.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<RenewalRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<RenewalRepositoryMoq>().Object);
    }

}
