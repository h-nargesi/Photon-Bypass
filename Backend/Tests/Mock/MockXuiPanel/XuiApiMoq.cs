using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Test;
using PhotonBypass.Tools;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Mock.MockXuiPanel;

internal class XuiApiMoq : Mock<IXuiApi>, IUnitLevelService
{
    public List<XuiClientModel> Clients { get; } = [];

    public List<XuiOnlineClientModel> OnlineClients { get; } = [];

    public Dictionary<string, (long Up, long Down)> Counters { get; } = [];

    public List<string> Calls { get; } = [];

    public XuiApiMoq()
    {
        Setup(api => api.ListClients(It.IsAny<ServerEntity>()))
            .Returns<ServerEntity>(_ => Task.FromResult(Clients.Select(Clone).ToList()));

        Setup(api => api.ListClientTraffic(It.IsAny<ServerEntity>()))
            .Returns<ServerEntity>(_ => Task.FromResult(Counters.Select(pair => new XuiClientTrafficModel
            {
                Email = pair.Key,
                Up = pair.Value.Up,
                Down = pair.Value.Down,
            }).ToList()));

        Setup(api => api.ListOnlineClients(It.IsAny<ServerEntity>()))
            .Returns<ServerEntity>(_ => Task.FromResult(OnlineClients.Select(Clone).ToList()));

        Setup(api => api.GetClient(It.IsAny<ServerEntity>(), It.IsAny<string>()))
            .Returns<ServerEntity, string>((_, email) => Task.FromResult(
                Clients.FirstOrDefault(client => client.Email == email) is { } found ? Clone(found) : null));

        Setup(api => api.AddClient(It.IsAny<ServerEntity>(), It.IsAny<XuiClientModel>()))
            .Callback<ServerEntity, XuiClientModel>((_, client) =>
            {
                Calls.Add($"add:{client.Email}");
                Clients.Add(Clone(client));
                Counters[client.Email] = (0, 0);
            })
            .Returns(Task.CompletedTask);

        Setup(api => api.UpdateClient(It.IsAny<ServerEntity>(), It.IsAny<string>(), It.IsAny<XuiClientModel>()))
            .Callback<ServerEntity, string, XuiClientModel>((_, currentUuid, client) =>
            {
                Calls.Add($"update:{client.Email}");

                var index = Clients.FindIndex(existing => existing.Uuid == currentUuid);

                if (index >= 0)
                {
                    Clients[index] = Clone(client);
                }
            })
            .Returns(Task.CompletedTask);

        Setup(api => api.RemoveClient(It.IsAny<ServerEntity>(), It.IsAny<string>()))
            .Callback<ServerEntity, string>((_, uuid) =>
            {
                var email = Clients.FirstOrDefault(client => client.Uuid == uuid)?.Email;

                if (email == null) return;

                Calls.Add($"remove:{email}");
                Clients.RemoveAll(client => client.Uuid == uuid);
                Counters.Remove(email);
            })
            .Returns(Task.CompletedTask);

        Setup(api => api.ResetClientTraffic(It.IsAny<ServerEntity>(), It.IsAny<string>()))
            .Callback<ServerEntity, string>((_, email) =>
            {
                Calls.Add($"reset:{email}");
                Counters[email] = (0, 0);
            })
            .Returns(Task.CompletedTask);
    }

    public void AddClient(string email, string uuid, long up, long down)
    {
        Clients.Add(new XuiClientModel { Email = email, Uuid = uuid });
        Counters[email] = (up, down);
    }

    private static XuiClientModel Clone(XuiClientModel client)
    {
        return new XuiClientModel
        {
            Email = client.Email,
            Uuid = client.Uuid,
            Enable = client.Enable,
            TotalTrafficBytes = client.TotalTrafficBytes,
            ExpiryTime = client.ExpiryTime,
            LimitIp = client.LimitIp,
        };
    }

    private static XuiOnlineClientModel Clone(XuiOnlineClientModel client)
    {
        return new XuiOnlineClientModel { Email = client.Email, Ips = new Dictionary<string, long>(client.Ips) };
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<XuiApiMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<XuiApiMoq>().Object);
    }
}
