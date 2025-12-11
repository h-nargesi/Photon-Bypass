using Moq;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Tools;
using System.Text.Json;

namespace PhotonBypass.Test.MockLocalRepository;

internal class RealmRepositoryMoq : Mock<IRealmRepository>, IOutSourceMoq
{
    public RealmRepositoryMoq() : this(FilePath)
    {
    }

    protected RealmRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data_dictionary = JsonSerializer.Deserialize<List<RealmEntity>>(raw_text)?
            .ToDictionary(k => k.Id) ?? [];

        Setup(repository => repository.GetName(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                if (!data_dictionary.TryGetValue(id, out var realm))
                {
                    realm = null;
                }

                return Task.FromResult(realm?.Name);
            });

        Setup(repository => repository.FetchAllActiveRealm())
            .Returns(() =>
            {
                var result = data_dictionary.Values.Where(realm => realm.IsActive).ToList();

                return Task.FromResult(result);
            });
    }

    private const string FilePath = "Data/realms.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<RealmRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<RealmRepositoryMoq>().Object);
    }

}
