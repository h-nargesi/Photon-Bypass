using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockLocalRepository;

public class ServerRepositoryMoq : Mock<IServerRepository>, IOutSourceMoq
{
    public ServerRepositoryMoq() : this(FilePath)
    {
    }

    protected ServerRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        var data_dictionary = JsonSerializer.Deserialize<List<ServerEntity>>(raw_text)?
                                  .GroupBy(server => server.Id)
                                  .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList())
                              ?? [];

        Setup(repository => repository.GetActiveRadiusInRealmOrAll(It.IsAny<int?>()))
            .Returns<int?>(realm_id =>
            {
                List<ServerEntity>? server_list;
                if (!realm_id.HasValue)
                {
                    server_list = data_dictionary.SelectMany(pair => pair.Value).ToList();
                }
                else if (!data_dictionary.TryGetValue(realm_id.Value, out server_list))
                {
                    server_list = [];
                }

                return Task.FromResult(server_list);
            });
    }

    private const string FilePath = "Data/server.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<ServerRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<ServerRepositoryMoq>().Object);
    }
}