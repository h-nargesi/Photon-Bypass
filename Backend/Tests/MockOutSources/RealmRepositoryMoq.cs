using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

class RealmRepositoryMoq : Mock<IRealmRepository>, IOutSourceMoq
{
    public Dictionary<int, ServerDensityEntity> Data { get; private set; } = null!;

    public event Action<int, RealmEntity?>? OnFetch;

    public event Action<int, List<ServerDensityEntity>?>? OnFetchServerDensityEntity;

    public RealmRepositoryMoq Setup(IDataSource source)
    {
        var raw_text = File.ReadAllText(source.FilePath);
        Data = System.Text.Json.JsonSerializer.Deserialize<List<ServerDensityEntity>>(raw_text)
            ?.ToDictionary(x => x.Id)
            ?? [];

        Setup(x => x.Fetch(It.IsNotNull<int>()))
            .Returns<int>(realm_id =>
            {
                if (!Data.TryGetValue(realm_id, out var entity))
                {
                    entity = null;
                }

                OnFetch?.Invoke(realm_id, entity);

                return Task.FromResult(entity as RealmEntity);
            });

        Setup(x => x.FetchServerDensityEntity(It.IsAny<int>()))
            .Returns<int>(cloud_id =>
            {
                var result = Data.Values.Where(x => x.CloudId == cloud_id)
                    .ToList();
                OnFetchServerDensityEntity?.Invoke(cloud_id, result);
                return Task.FromResult(result);
            });

        return this;
    }

    IOutSourceMoq IOutSourceMoq.Setup(IDataSource source)
    {
        return Setup(source);
    }

    public class DataSource : IDataSource
    {
        public string FilePath { get; set; } = "Data/realms.json";
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped(s => new DataSource());
        services.AddScoped(s => new RealmRepositoryMoq().Setup(s.GetRequiredService<DataSource>()));
        services.AddLazyScoped(s => s.GetRequiredService<RealmRepositoryMoq>().Object);
    }
}
