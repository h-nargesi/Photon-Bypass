using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;
using System.Text.Json;

namespace PhotonBypass.Test.Facts.Scenario;

/// <summary>
/// Admin wallet transaction and wallet history over real HTTP (docs/20 phase
/// 3, D5): the admin-only adjust endpoint records an append-only Completed
/// row with the acting admin, negative balances are allowed, and history is
/// visible only to the owner (independent) or the admin — never a manager.
/// </summary>
public class WalletAdminTransactionTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task Adjust_ByAdmin_RecordsCompletedRowWithActor()
    {
        var target = "wal_target01";

        await InsertAccount(target, UserTypes.None);
        await Login("admin", "admin");

        await WalletAdjust(target, (short)BalanceDirection.Credit, 5_000, "پرداخت حضوری");

        using var scope = ServiceProvider.CreateScope();
        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

        var ids = await account_repo.GetAccountIdByUsername(["admin", target]);

        var row = Assert.Single(await wallet_repo.GetTransactions(ids[target]), w => w.ActorId != null);
        Assert.Equal(BalanceStatus.Completed, row.Status);
        Assert.Equal(BalanceDirection.Credit, row.Direction);
        Assert.Equal(5_000, row.Amount);
        Assert.Equal(ids["admin"], row.ActorId);
        Assert.Equal(5_000, await wallet_repo.GetBalance(ids[target]));

        // a debit beyond the balance is allowed — the wallet may go negative (D5)
        await WalletAdjust(target, (short)BalanceDirection.Debit, 6_000, "تصحیح حساب");

        Assert.Equal(-1_000, await wallet_repo.GetBalance(ids[target]));
    }

    [Fact]
    public async Task Adjust_ValidationAndPolicy()
    {
        await Login("admin", "admin");

        await WalletAdjust(null, (short)BalanceDirection.Credit, 0, "d", status: 4);
        await WalletAdjust(null, (short)BalanceDirection.Credit, -5, "d", status: 4);
        await WalletAdjust(null, (short)BalanceDirection.Credit, 100, " ", status: 4);
        await WalletAdjust(null, 0, 100, "d", status: 4);
        await WalletAdjust(null, (short)BalanceDirection.Credit, 100, new string('d', 200), status: 4);
        await WalletAdjust("wal_ghost02", (short)BalanceDirection.Credit, 100, "d", status: 4);

        // adjust is admin-only (docs/20 §3): a plain user is refused by policy
        await Register("wal_user03", "62", "Password");
        await Login("wal_user03", "Password");

        await WalletAdjust(null, (short)BalanceDirection.Credit, 100, "d", status: 403);
        await WalletAdjust("wal_user03", (short)BalanceDirection.Credit, 100, "d", status: 403);
    }

    [Fact]
    public async Task WalletHistory_AccessMatrixAndContent()
    {
        var target = "wal_target04";

        await InsertAccount(target, UserTypes.None);
        await Login("admin", "admin");

        await WalletAdjust(target, (short)BalanceDirection.Credit, 2_000, "کیف پول اولیه");

        // admin on-behalf view carries the manual row with the actor username
        var admin_view = await WalletHistory(target);

        Assert.Equal("1", admin_view["totalCount"]);
        Assert.Equal("1", admin_view["totalPages"]);

        var item = Assert.Single(ParseItems(admin_view));
        Assert.Equal("2000", item["amount"]);
        Assert.Equal(((short)BalanceDirection.Credit).ToString(), item["direction"]);
        Assert.Equal("admin", item["actor"]);

        // the dependent owner is refused its own wallet history (D6)
        await Login(target, "Password");

        await WalletHistory(status: 403);

        // an independent account sees its own (empty) history
        await Register("wal_indep05", "63", "Password");
        await Login("wal_indep05", "Password");

        var self = await WalletHistory();
        Assert.Equal("0", self["totalCount"]);

        // on-behalf view without target access is refused by LoadJobContext…
        await WalletHistory(target, status: 403);

        // …and a manager passes target access but is still refused the wallet
        // itself (docs/20 §3: managers never see a subordinate's wallet)
        var manager = "wal_manager06";
        var subordinate = "wal_sub07";

        using (var scope = ServiceProvider.CreateScope())
        {
            var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

            await InsertAccount(manager, UserTypes.AllowSelfService, AccountRole.Manager);

            var manager_id = (await account_repo.GetAccountIdByUsername([manager]))[manager];

            await InsertAccount(subordinate, UserTypes.None, owner_id: manager_id);
        }

        await Login(manager, "Password");

        await WalletHistory(subordinate, status: 403);
    }

    [Fact]
    public async Task WalletHistory_ServerSidePaging()
    {
        var owner = "wal_pager08";

        await Register(owner, "64", "Password");

        using (var scope = ServiceProvider.CreateScope())
        {
            var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

            var id = (await account_repo.GetAccountIdByUsername([owner]))[owner];

            for (var i = 0; i < 23; i++)
            {
                await wallet_repo.Save(new WalletEntity
                {
                    AccountId = id,
                    Amount = 1_000,
                    Direction = BalanceDirection.Credit,
                    Status = BalanceStatus.Completed,
                    Description = $"seed {i}",
                });
            }
        }

        await Login(owner, "Password");

        var first = await WalletHistory();

        Assert.Equal("23", first["totalCount"]);
        Assert.Equal("2", first["totalPages"]);
        Assert.Equal(20, ParseItems(first).Count);

        var second = await WalletHistory(page: 2);

        Assert.Equal(3, ParseItems(second).Count);

        await WalletHistory(page: 0, status: 4);
    }

    private static List<Dictionary<string, string?>> ParseItems(Dictionary<string, string?> page)
    {
        return JsonSerializer.Deserialize<JsonElement>(page["items"]!)
            .EnumerateArray()
            .Select(element => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.ToString()))
            .ToList();
    }
}
