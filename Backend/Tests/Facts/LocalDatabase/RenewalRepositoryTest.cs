using FluentAssertions;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.LocalDatabase;

/// <summary>
/// Real-SQL regression test for the latest-renewal lookup used by the x-ui
/// collector on every cycle (docs/14 M31): the ROW_NUMBER rewrite must return
/// exactly one row per account — the newest by identity.
/// </summary>
public class RenewalRepositoryTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "RenewalLatest";

    [Fact]
    public async Task GetLatestByAccountIds_ReturnsNewestRenewalPerAccount()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var renewal_repo = scope.ServiceProvider.GetRequiredService<IRenewalRepository>();

        var accounts = await account_repo.GetAccountIdByUsername(["RenewA", "RenewB"]);
        var renew_a = accounts["RenewA"];
        var renew_b = accounts["RenewB"];

        var result = await renewal_repo.GetLatestByAccountIds([renew_a, renew_b, 99001]);

        result.Keys.Should().BeEquivalentTo([renew_a, renew_b]);

        // RenewA has a history: only the newest renewal comes back
        result[renew_a].Comment.Should().Be("T-renew-a-new");
        result[renew_a].SimultaneousUser.Should().Be(2);

        result[renew_b].Comment.Should().Be("T-renew-b-only");

        (await renewal_repo.GetLatestByAccountIds([])).Should().BeEmpty();
    }
}
