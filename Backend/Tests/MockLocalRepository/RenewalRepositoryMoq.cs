using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Tools;
using System.Text.Json;
using PhotonBypass.Test.MockOptions;

namespace PhotonBypass.Test.MockLocalRepository;

internal class RenewalRepositoryMoq : Mock<IRenewalRepository>, IOutSourceMoq
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
