using Moq;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Tools;
using System.Text.Json;

namespace PhotonBypass.Test.MockLocalRepository;

internal class RenewalRepositoryMoq : Mock<IRenewalRepository>, IOutSourceMoq
{
    public RenewalRepositoryMoq() : this(FilePath)
    {
    }

    protected RenewalRepositoryMoq(string file_path)
    {
        var raw_text = File.Exists(file_path) ? File.ReadAllText(file_path) : null;
        var data_dictionary = raw_text != null ? JsonSerializer.Deserialize<List<RenewalEntity>>(raw_text)
            ?.ToDictionary(k => k.AccountId) ?? [] : [];

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

    private const string FilePath = "Data/renewal.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<RenewalRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<RenewalRepositoryMoq>().Object);
    }

}
