using System.Net.Http.Json;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class DownloadCert(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task Download_AfterRenewal_ReturnsOvpnFile()
    {
        // /api/auth/register
        await Register("userdl", "70");

        // /api/auth/token
        await Login("userdl", "Password");

        // /api/plan/estimate + wallet top-up + /api/plan/renewal
        var request = new RenewalContext
        {
            Target = "userdl",
            SimultaneousUserCount = 1,
            Days = 120,
            Gigabytes = 25,
        };

        var response = await Client.PostAsJsonAsync("/api/plan/estimate", request);
        var estimate = await CheckResponseObject(response);

        await FakeAddMoney("userdl", int.Parse(estimate["price"]!.ToString()!));

        response = await Client.PostAsJsonAsync("/api/plan/renewal", request);
        await CheckResponse(response);

        // /api/vpn/download-cert
        var cert_response = await DownloadCert();

        Assert.Equal("application/x-openvpn-profile", cert_response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", cert_response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(".ovpn", cert_response.Content.Headers.ContentDisposition?.FileName);

        var cert_file = await cert_response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(cert_file);

        // the download is a credential recovery: it must leave an audit row behind
        using (var scope = ServiceProvider.CreateScope())
        {
            var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var history_repo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
            var account = await account_repo.GetAccount("userdl");
            Assert.NotNull(account);

            var history = await history_repo.GetHistory(account.Id.ToString(), null, null);
            Assert.Contains(history, row => row.Description == "کانفیگ اتصال دانلود شد.");
        }

        // /api/vpn/download-cert for a foreign target is rejected by the access gate
        await Register("userdl2", "71");
        await DownloadCert("userdl2", status: 4);
    }
}
