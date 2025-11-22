using System.Text.Json;
using Moq;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

internal class RealmRepositoryMoq : Mock<IRealmRepository>, IOutSourceMoq
{
    public event Action<int, RealmEntity?>? OnFetch;

    public event Action<int, List<ServerDensityEntity>?>? OnFetchServerDensityEntity;

    public RealmRepositoryMoq() : this(FilePath)
    {
    }

    protected RealmRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data = JsonSerializer.Deserialize<List<ServerDensityEntity>>(raw_text)
                   ?.ToDictionary(x => x.Id)
               ?? [];

        Setup(x => x.Fetch(It.IsNotNull<int>()))
            .Returns<int>(realm_id =>
            {
                if (!data.TryGetValue(realm_id, out var entity))
                {
                    entity = null;
                }

                OnFetch?.Invoke(realm_id, entity);

                return Task.FromResult(entity as RealmEntity);
            });

        Setup(x => x.FetchServerDensityEntity(It.IsAny<int>()))
            .Returns<int>(cloud_id =>
            {
                var result = data.Values.Where(x => x.CloudId == cloud_id)
                    .ToList();
                OnFetchServerDensityEntity?.Invoke(cloud_id, result);
                return Task.FromResult(result);
            });
    }

    private const string FilePath = "Data/realms.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<RealmRepositoryMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<RealmRepositoryMoq>().Object);
    }
}