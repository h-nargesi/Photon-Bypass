using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class VpnConfigEndpoints(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task ConfigInfoAndDownload_AfterRenewal_ServeTheRadiusDataPlane()
    {
        // /api/auth/register + /api/auth/token
        await Register("usercfg", "80");
        await Login("usercfg", "Passw0rd1");

        // /api/plan/new-account — the first purchase creates the VPN account
        // (docs/22 D9); realm 1 is the scenario realm
        var renewal = await NewAccount(null, 1, 1, 120, 25);
        var code = int.Parse(renewal["invoiceCode"]!);

        var invoice = await GetInvoice(code);
        await FakeAddMoney("usercfg", int.Parse(invoice["totalPrice"]!));

        // /api/billing/settle-wallet
        await SettleWallet(code);

        var account_id = int.Parse((await VpnAccounts()).Single()["id"]!);

        // /api/vpn/config-info — the scenario fleet is UserManager: kind only, no link
        var response = await Client.GetAsync($"/api/vpn/config-info?accountId={account_id}");
        var info = await CheckResponseObject(response);

        Assert.Equal("usermanager", info["kind"]);
        Assert.Null(info["vlessLink"]);

        // /api/vpn/download — OVPN data-plane: ZIP with cert + connection sheet
        response = await Client.GetAsync($"/api/vpn/download?accountId={account_id}");

        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(".zip", response.Content.Headers.ContentDisposition?.FileName);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());

        // /api/vpn/config-info for a foreign account is rejected by the access gate
        await Register("usercfg2", "81");

        int foreign_account_id;

        using (var scope = ServiceProvider.CreateScope())
        {
            var panel_user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
            var vpn_account_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();

            var foreign_id = await panel_user_repo.GetActiveUserId("usercfg2");

            await vpn_account_repo.Save(new VpnAccountEntity
            {
                UserId = foreign_id!.Value,
                RealmId = 1,
                Username = "usercfg2",
                VpnPassword = "VpnPassword",
            });

            foreign_account_id = (await vpn_account_repo.GetAccount("usercfg2"))!.Id;
        }

        response = await Client.GetAsync($"/api/vpn/config-info?accountId={foreign_account_id}");
        await CheckResponse(response, 403);
    }
}
