using System.IO.Compression;
using System.Net.Http.Json;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class DownloadConfig(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task Download_AfterRenewal_ReturnsOvpnZip()
    {
        // /api/auth/register
        await Register("userdl", "70");

        // /api/auth/token
        await Login("userdl", "Passw0rd1");

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

        // /api/vpn/download — OVPN data-plane: ZIP with cert + connection sheet
        var download_response = await DownloadConfig();

        Assert.Equal("application/zip", download_response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download_response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(".zip", download_response.Content.Headers.ContentDisposition?.FileName);

        using var archive = new ZipArchive(new MemoryStream(await download_response.Content.ReadAsByteArrayAsync()));
        Assert.Contains(archive.Entries, entry => entry.FullName == "config.ovpn");
        Assert.Contains(archive.Entries, entry => entry.FullName == "connection-info.txt");

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

        // /api/vpn/download for a foreign target is rejected by the access gate
        await Register("userdl2", "71");
        await DownloadConfig("userdl2", status: 4);
    }
}
