using System.Text.Json;
using FluentAssertions;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Live client lifecycle on the Xui-Test VM (Docs/23 §4.4): the full add →
/// update (UUID rotation / quota overwrite) → reset → remove cycle against
/// the real alireza0/x-ui v1.14.0 panel. Every test owns a unique email and
/// removes its client in a finally; the snapshot restore at the start of the
/// next run stays the ultimate sanitizer.
/// </summary>
[Collection(XuiPanelTestCollection.Name)]
public class XuiPanelClientLifecycleTest : OutSourceLevelServiceInitializer
{
    [Fact]
    public async Task AddClient_ClientAppearsWithPushedFields_AndBornStatsRow()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("lc-add");
        var uuid = Guid.NewGuid().ToString();
        var expiry = 1_900_000_000_000L;

        try
        {
            await api.Value.AddClient(server, Client(email, uuid,
                total_gb: 26_843_545_600L, expiry, limit_ip: 2));

            var client = (await api.Value.ListClients(server))
                .Should().ContainSingle(c => c.Email == email).Subject;

            client.Uuid.Should().Be(uuid);
            client.Enable.Should().BeTrue();
            client.TotalTrafficBytes.Should().Be(26_843_545_600L);
            client.ExpiryTime.Should().Be(expiry);
            client.LimitIp.Should().Be(2);

            var stats = (await api.Value.ListClientTraffic(server))
                .Should().ContainSingle(s => s.Email == email).Subject;

            stats.Up.Should().Be(0);
            stats.Down.Should().Be(0);
        }
        finally
        {
            await api.Value.RemoveClient(server, uuid);
        }
    }

    [Fact]
    public async Task AddClient_DuplicateEmail_IsRejectedByPanel()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("lc-dup");
        var uuid = Guid.NewGuid().ToString();

        try
        {
            await api.Value.AddClient(server, Client(email, uuid));

            var action = () => api.Value.AddClient(server, Client(email, Guid.NewGuid().ToString()));

            await action.Should().ThrowAsync<Exception>();
        }
        finally
        {
            await api.Value.RemoveClient(server, uuid);
        }
    }

    [Fact]
    public async Task UpdateClient_UuidRotation_SwapsKey_KeepsEmailAndStatsRow()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("lc-rot");
        var old_uuid = Guid.NewGuid().ToString();
        var new_uuid = Guid.NewGuid().ToString();

        try
        {
            await api.Value.AddClient(server, Client(email, old_uuid));

            await api.Value.UpdateClient(server, old_uuid, Client(email, new_uuid));

            var clients = await api.Value.ListClients(server);

            clients.Should().Contain(c => c.Uuid == new_uuid && c.Email == email);
            clients.Should().NotContain(c => c.Uuid == old_uuid);

            // the email-keyed statistics row survives the key rotation
            (await api.Value.ListClientTraffic(server))
                .Should().ContainSingle(s => s.Email == email);
        }
        finally
        {
            await api.Value.RemoveClient(server, new_uuid);
        }
    }

    [Fact]
    public async Task UpdateClient_OverwritesQuotaExpiryLimitIp()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("lc-quota");
        var uuid = Guid.NewGuid().ToString();
        var new_expiry = 1_950_000_000_000L;

        try
        {
            await api.Value.AddClient(server, Client(email, uuid,
                total_gb: 26_843_545_600L, expiry: 1_900_000_000_000L, limit_ip: 1));

            await api.Value.UpdateClient(server, uuid, Client(email, uuid,
                total_gb: 53_687_091_200L, expiry: new_expiry, limit_ip: 3));

            var client = (await api.Value.ListClients(server))
                .Should().ContainSingle(c => c.Email == email).Subject;

            client.TotalTrafficBytes.Should().Be(53_687_091_200L);
            client.ExpiryTime.Should().Be(new_expiry);
            client.LimitIp.Should().Be(3);
        }
        finally
        {
            await api.Value.RemoveClient(server, uuid);
        }
    }

    [Fact]
    public async Task ResetClientTraffic_KeyedByEmail_KeepsClientAndStatsRow()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("lc-reset");
        var uuid = Guid.NewGuid().ToString();

        try
        {
            await api.Value.AddClient(server, Client(email, uuid));

            await api.Value.ResetClientTraffic(server, email);

            (await api.Value.ListClients(server))
                .Should().ContainSingle(c => c.Email == email);
            (await api.Value.ListClientTraffic(server))
                .Should().ContainSingle(s => s.Email == email);
        }
        finally
        {
            await api.Value.RemoveClient(server, uuid);
        }
    }

    [Fact]
    public async Task RemoveClient_RemovesClientAndStatsRow()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("lc-del");
        var uuid = Guid.NewGuid().ToString();

        await api.Value.AddClient(server, Client(email, uuid));
        (await api.Value.ListClientTraffic(server))
            .Should().ContainSingle(s => s.Email == email);

        await api.Value.RemoveClient(server, uuid);

        (await api.Value.ListClients(server)).Should().NotContain(c => c.Email == email);
        (await api.Value.ListClientTraffic(server)).Should().NotContain(s => s.Email == email);
    }

    [Fact]
    public async Task GetInbound_ReflectsConfiguredVlessWsInbound()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);

        var inbound = await api.Value.GetInbound(server);

        inbound.Id.Should().Be(XuiPanelTestServer.Vm.InboundId);
        inbound.Protocol.Should().Be("vless");
        inbound.Port.Should().BeGreaterThan(0);

        var stream = JsonDocument.Parse(inbound.StreamSettings!).RootElement;

        stream.GetProperty("network").GetString().Should().Be("ws");
        stream.GetProperty("security").GetString().Should().Be("none");
        stream.GetProperty("wsSettings").GetProperty("path").GetString().Should().NotBeNullOrEmpty();
    }

    private static XuiClientModel Client(
        string email, string uuid, long total_gb = 0, long expiry = 0, int limit_ip = 0)
    {
        return new XuiClientModel
        {
            Email = email,
            Uuid = uuid,
            Enable = true,
            TotalTrafficBytes = total_gb,
            ExpiryTime = expiry,
            LimitIp = limit_ip,
        };
    }
}
