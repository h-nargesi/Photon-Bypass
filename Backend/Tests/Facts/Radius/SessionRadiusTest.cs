using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Test.Initializer;
using tik4net.Objects;

namespace PhotonBypass.Test.Facts.Radius;

public class SessionRadiusTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task GetTrafficData_DisableRealm_OnlyRadiusServersAreQueried()
    {
        using var scope = App.Services.CreateScope();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();
        var session_radius_sync_srv = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();
        var tik4_handler = scope.ServiceProvider.GetRequiredService<ITik4NetHandler>();

        var radius_list = await server_repo.GetAllActiveRadius();

        // Server2 (realm 2) carries no Radius feature bits in the seed — a
        // disabled/non-radius realm must not take part in the traffic sync
        Assert.DoesNotContain(radius_list, server => server.RealmId == 2);

        var traffics = await session_radius_sync_srv.GetTrafficData(radius_list, []);

        Assert.NotNull(traffics);

        // every session of the radius servers (no time window) is returned
        var expected_count =
            CountSessionsOf(tik4_handler, 1) +
            CountSessionsOf(tik4_handler, 3) +
            CountSessionsOf(tik4_handler, 4);
        Assert.Equal(expected_count, traffics.Count);

        // sessions that exist only on the excluded realm-2 server must not leak in:
        // User2@today-2 10:00 rows exist solely on Server2
        var realm2_only_start = DateTime.Now.Date.AddDays(-2).AddHours(10);
        Assert.DoesNotContain(traffics, traffic =>
            traffic.Username == "User2" && traffic.StartSession == realm2_only_start);

        // positive control: the still-open realm-3 session is present
        Assert.Contains(traffics, traffic => traffic.Username == "User3" && traffic.EndSession == null);
    }

    [Fact]
    public async Task GetTrafficData_TimeLimit_IndexWindowsEachRealm()
    {
        using var scope = App.Services.CreateScope();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();
        var session_radius_sync_srv = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();

        var radius_list = await server_repo.GetAllActiveRadius();

        // realm 1: keep everything from day -5 on; realm 3: from day -4 on;
        // realm 4: null index → no window (full history)
        var indexes = new Dictionary<int, DateTime?>
        {
            [1] = DateTime.Now.Date.AddDays(-5),
            [3] = DateTime.Now.Date.AddDays(-4),
            [4] = null,
        };

        var traffics = await session_radius_sync_srv.GetTrafficData(radius_list, indexes);

        Assert.NotNull(traffics);

        var realm1_rows = traffics.Where(t => t.NasIpAddress == "192.168.125.11").ToArray();
        var realm3_rows = traffics.Where(t => t.NasIpAddress == "192.168.125.31").ToArray();
        var realm4_rows = traffics.Where(t => t.NasIpAddress == "192.168.125.41").ToArray();

        // realm 1 window (sessions after day -5): 3× day-4 + 1× day-3
        Assert.Equal(4, realm1_rows.Length);
        Assert.All(realm1_rows, t =>
            Assert.True(t.StartSession > DateTime.Now.Date.AddDays(-5).AddSeconds(-1),
                $"realm-1 row before window: {t.StartSession:s}"));

        // realm 3 window (sessions after day -4): 3× day-4 + 1× day-3 + 1× day-2 + 1× now
        Assert.Equal(6, realm3_rows.Length);
        Assert.All(realm3_rows, t =>
            Assert.True(t.StartSession > DateTime.Now.Date.AddDays(-4).AddSeconds(-1),
                $"realm-3 row before window: {t.StartSession:s}"));

        // the boundary row (day-4 10:00) is included: strictly after cutoff minus one second
        Assert.Contains(realm3_rows, t =>
            t.StartSession == DateTime.Now.Date.AddDays(-4).AddHours(10));

        // realm 4 has a null index → full history (6 seeded rows)
        Assert.Equal(6, realm4_rows.Length);

        // rows from outside the windows (day -13..-11) are gone for realms 1 and 3
        var old_cutoff = DateTime.Now.Date.AddDays(-11);
        Assert.DoesNotContain(traffics, t =>
            (t.NasIpAddress == "192.168.125.11" || t.NasIpAddress == "192.168.125.31") &&
            t.StartSession < old_cutoff);
    }

    private static int CountSessionsOf(ITik4NetHandler handler, int server_id)
    {
        var server = new ServerEntity { Id = server_id, Name = $"Server{server_id}" };

        using var connection = handler.ConnectTo(server).Result;
        return connection.LoadList<SessionModel>().Count();
    }
}
