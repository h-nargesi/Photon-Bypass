using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class ServerRepositoryMoq : Mock<IServerRepository>, IUnitLevelService
{
    public readonly Dictionary<int, ServerEntity> Data;

    public ServerRepositoryMoq(TransactionalMockDbContext db_context) : this(db_context, FilePath)
    {
    }

    protected ServerRepositoryMoq(TransactionalMockDbContext db_context, string file_path)
    {
        var raw_text = File.ReadAllText(file_path)
            .PrepareAllDateTimes();
        Data = JsonSerializer.Deserialize<List<ServerEntity>>(raw_text)?
                   .GroupBy(server => server.Id)
                   .ToDictionary(grouping => grouping.Key, grouping => grouping.First())
               ?? [];

        Setup(repository => repository.GetAllServers())
            .Returns(() =>
            {
                var server_list = Data.Values.OrderBy(server => server.Id).ToList();

                return Task.FromResult(server_list);
            });

        Setup(repository => repository.GetServerById(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                Data.TryGetValue(id, out var server);

                return Task.FromResult(server);
            });

        Setup(repository => repository.FindConflicts(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns<string, string, string, int>((ip, name, domain, exclude_id) =>
            {
                var server_list = Data.Values
                    .Where(server => server.Id != exclude_id &&
                                     (server.IpAddress.Equals(ip, StringComparison.OrdinalIgnoreCase) ||
                                      server.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                                      server.DomainName.Equals(domain, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                return Task.FromResult(server_list);
            });

        Setup(repository => repository.Save(It.IsAny<ServerEntity>()))
            .Returns<ServerEntity>(server =>
            {
                if (server.Id < 1)
                {
                    server.Id = Data.Values.Select(s => s.Id).DefaultIfEmpty(0).Max() + 1;
                    Data[server.Id] = server;
                    db_context.RegisterUndo(() => Data.Remove(server.Id));
                }
                else if (Data.TryGetValue(server.Id, out var stored))
                {
                    var snapshot = new ServerEntity
                    {
                        Id = stored.Id,
                        IsActive = stored.IsActive,
                        RealmId = stored.RealmId,
                        IpAddress = stored.IpAddress,
                        Name = stored.Name,
                        DomainName = stored.DomainName,
                        BandWidth = stored.BandWidth,
                        OsType = stored.OsType,
                        Features = stored.Features,
                        JsonConfig = stored.JsonConfig,
                    };

                    db_context.RegisterUndo(() =>
                    {
                        stored.IsActive = snapshot.IsActive;
                        stored.RealmId = snapshot.RealmId;
                        stored.IpAddress = snapshot.IpAddress;
                        stored.Name = snapshot.Name;
                        stored.DomainName = snapshot.DomainName;
                        stored.BandWidth = snapshot.BandWidth;
                        stored.OsType = snapshot.OsType;
                        stored.Features = snapshot.Features;
                        stored.JsonConfig = snapshot.JsonConfig;
                    });

                    stored.IsActive = server.IsActive;
                    stored.RealmId = server.RealmId;
                    stored.IpAddress = server.IpAddress;
                    stored.Name = server.Name;
                    stored.DomainName = server.DomainName;
                    stored.BandWidth = server.BandWidth;
                    stored.OsType = server.OsType;
                    stored.Features = server.Features;
                    stored.JsonConfig = server.JsonConfig;
                }

                return Task.CompletedTask;
            });

        Setup(repository => repository.GetAllActiveRadius())
            .Returns(() =>
            {
                var server_list = Data.Values
                    .Where(server => (server.Features & ServerFeature.Radius) != 0)
                    .ToList();

                return Task.FromResult(server_list);
            });

        Setup(repository => repository.GetAllActiveNasInRealm(It.IsAny<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(realm_ids =>
            {
                var mask_hash = realm_ids.ToHashSet();

                var server_list = Data.Values
                    .Where(server => mask_hash.Contains(server.RealmId) &&
                                     (server.Features & ServerFeature.Nas) != 0)
                    .GroupBy(k => k.RealmId)
                    .ToDictionary(k => k.Key, k => k.ToList());

                return Task.FromResult(server_list);
            });

        Setup(repository => repository.GetActiveNasInRealmOrAll(It.IsAny<int?>()))
            .Returns<int?>(realm_id =>
            {
                List<ServerEntity> server_list;
                if (!realm_id.HasValue)
                {
                    server_list = [.. Data.Values];
                }
                else
                {
                    server_list = Data.Values.Where(s => s.RealmId == realm_id.Value).ToList();
                }

                server_list = server_list.Where(s => (s.Features & ServerFeature.Nas) != 0).ToList();

                return Task.FromResult(server_list);
            });

        Setup(repository => repository.GetAllActiveNasDomainInRealm(It.IsAny<int?>()))
            .Returns<int?>(realm_id =>
            {
                var domain_name_list = Data.Values
                    .Where(server => (server.RealmId == realm_id || realm_id == null) &&
                                     (server.Features & ServerFeature.Nas) != 0)
                    .Select(nas => nas.DomainName)
                    .ToList();

                return Task.FromResult(domain_name_list);
            });

        Setup(repository => repository.GetActiveNasInfo(It.IsAny<string>()))
            .Returns<string>(ip =>
            {
                var server = Data.Values
                    .FirstOrDefault(server => server.IpAddress == ip &&
                                              (server.Features & ServerFeature.Nas) != 0);

                return Task.FromResult(server);
            });

        Setup(repository => repository.GetServerIdByIpAddress(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(ips =>
            {
                var hash_mask = ips.ToHashSet();

                var server_ip_id = Data.Values
                    .Where(server => hash_mask.Contains(server.IpAddress))
                    .ToDictionary(server => server.IpAddress, server => (server.Id, server.RealmId));

                return Task.FromResult(server_ip_id);
            });

        Setup(repository => repository.GetActiveRadiusInRealmOrAll(It.IsAny<int?>()))
            .Returns<int?>(realm_id =>
            {
                List<ServerEntity> server_list;
                if (!realm_id.HasValue)
                {
                    server_list = [.. Data.Values];
                }
                else
                {
                    server_list = Data.Values.Where(s => s.RealmId == realm_id.Value).ToList();
                }

                server_list = server_list.Where(s => (s.Features & ServerFeature.Radius) != 0).ToList();

                return Task.FromResult(server_list);
            });

        Setup(repository => repository.DbContext).Returns(db_context);
    }

    private const string FilePath = "Data/Local/server.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<ServerRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<ServerRepositoryMoq>().Object);
    }
}
