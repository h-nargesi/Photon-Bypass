using System.IO.Compression;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
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

        // /api/plan/new-account — the first purchase creates the VPN account
        // (Docs/22 D9); realm 1 is the scenario realm
        var renewal = await NewAccount(null, 1, 1, 1, 120, 25);
        var code = int.Parse(renewal["invoiceCode"]!);

        var invoice = await GetInvoice(code);
        await FakeAddMoney("userdl", int.Parse(invoice["totalPrice"]!));

        // /api/billing/settle-wallet
        await SettleWallet(code);

        var account_id = int.Parse((await VpnAccounts()).Single()["id"]!);

        // /api/vpn/download — OVPN data-plane: ZIP with cert + connection sheet
        var download_response = await DownloadConfig(account_id);

        Assert.Equal("application/zip", download_response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download_response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(".zip", download_response.Content.Headers.ContentDisposition?.FileName);

        using var archive = new ZipArchive(new MemoryStream(await download_response.Content.ReadAsByteArrayAsync()));
        Assert.Contains(archive.Entries, entry => entry.FullName == "config.ovpn");
        Assert.Contains(archive.Entries, entry => entry.FullName == "connection-info.txt");

        // the download is a credential recovery: it must leave an audit row behind
        using (var scope = ServiceProvider.CreateScope())
        {
            var panel_user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
            var vpn_account_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();
            var history_repo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
            var account = await vpn_account_repo.GetAccount("userdl");
            Assert.NotNull(account);

            var user_id = (await panel_user_repo.GetActiveUserId("userdl"))!.Value;

            var history = await history_repo.GetHistory(user_id, null, null);
            Assert.Contains(history, row => row.Description == "کانفیگ اتصال دانلود شد.");
        }

        // /api/vpn/download for a foreign account is rejected by the access gate
        await Register("userdl2", "71");

        int foreign_account_id;

        using (var scope = ServiceProvider.CreateScope())
        {
            var panel_user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
            var vpn_account_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();

            var foreign_id = await panel_user_repo.GetActiveUserId("userdl2");

            await vpn_account_repo.Save(new VpnAccountEntity
            {
                UserId = foreign_id!.Value,
                RealmId = 1,
                Username = "userdl2",
                VpnPassword = "VpnPassword",
            });

            foreign_account_id = (await vpn_account_repo.GetAccount("userdl2"))!.Id;
        }

        await DownloadConfig(foreign_account_id, status: 403);
    }
}
