using System.Net.Http.Json;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class VpnConfigEndpoints(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task ConfigInfoAndDownload_AfterRenewal_ServeTheRadiusDataPlane()
    {
        // /api/auth/register + /api/auth/token
        await Register("usercfg", "80");
        await Login("usercfg", "Password");

        // /api/plan/estimate + wallet top-up + /api/plan/renewal
        var request = new RenewalContext
        {
            Target = "usercfg",
            SimultaneousUserCount = 1,
            Days = 120,
            Gigabytes = 25,
        };

        var response = await Client.PostAsJsonAsync("/api/plan/estimate", request);
        var estimate = await CheckResponseObject(response);

        await FakeAddMoney("usercfg", int.Parse(estimate["price"]!.ToString()!));

        response = await Client.PostAsJsonAsync("/api/plan/renewal", request);
        await CheckResponse(response);

        // /api/vpn/config-info — the scenario fleet is UserManager: kind only, no link
        response = await Client.GetAsync("/api/vpn/config-info");
        var info = await CheckResponseObject(response);

        Assert.Equal("usermanager", info["kind"]);
        Assert.Null(info["vlessLink"]);

        // /api/vpn/download — same OVPN payload as the legacy download-cert
        var download_response = await Client.GetAsync("/api/vpn/download");

        Assert.Equal("application/x-openvpn-profile", download_response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download_response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(".ovpn", download_response.Content.Headers.ContentDisposition?.FileName);
        Assert.NotEmpty(await download_response.Content.ReadAsByteArrayAsync());

        // /api/vpn/config-info for a foreign target is rejected by the access gate
        await Register("usercfg2", "81");
        response = await Client.GetAsync("/api/vpn/config-info?target=usercfg2");
        await CheckResponse(response, 4);
    }
}
