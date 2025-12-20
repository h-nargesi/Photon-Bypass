using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class RealmRepositoryMoq : Mock<IRealmRepository>, IUnitLevelService
{
    public RealmRepositoryMoq() : this(FilePath)
    {
    }

    protected RealmRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
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

        Setup(repository => repository.GetByIds(It.IsAny<List<int>>()))
            .Returns<List<int>>((ids) =>
            {
                var mask = ids.ToHashSet();
                var result = data_dictionary.Values.Where(realm => mask.Contains(realm.Id))
                    .ToDictionary(r => r.Id);

                return Task.FromResult(result);
            });
    }

    private const string FilePath = "Data/Local/realms.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<RealmRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<RealmRepositoryMoq>().Object);
    }

}
