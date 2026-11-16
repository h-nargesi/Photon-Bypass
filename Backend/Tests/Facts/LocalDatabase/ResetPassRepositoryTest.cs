using FluentAssertions;
using PhotonBypass.Domain.Account;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.LocalDatabase;

/// <summary>
/// Real-SQL regression test for the raw UPDATE behind the reset-code
/// invalidation (docs/14 M44): only the still-active codes of the target
/// account may be expired — other accounts and already-expired rows stay
/// exactly as they were.
/// </summary>
public class ResetPassRepositoryTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "ResetPassExpire";

    [Fact]
    public async Task ExpireActiveCodes_ExpiresOnlyTheActiveCodesOfThatAccount()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
        var reset_repo = scope.ServiceProvider.GetRequiredService<IResetPassRepository>();

        var reset_a = await user_repo.GetActiveUserId("ResetA");
        var reset_b = await user_repo.GetActiveUserId("ResetB");
        Assert.NotNull(reset_a);
        Assert.NotNull(reset_b);

        await reset_repo.ExpireActiveCodes(reset_a!.Value);

        var after_expire = DateTime.Now;

        // both live codes of the target account are expired now
        (await reset_repo.GetAccount("T-reset-a-live-1"))!.ExpireDate.Should().BeOnOrBefore(after_expire);
        (await reset_repo.GetAccount("T-reset-a-live-2"))!.ExpireDate.Should().BeOnOrBefore(after_expire);

        // the already-expired code keeps its original (older) date: the WHERE
        // clause must not rewrite rows it does not need to touch
        (await reset_repo.GetAccount("T-reset-a-old"))!.ExpireDate.Should().BeBefore(after_expire.AddDays(-4));

        // another account's live code stays valid
        (await reset_repo.GetAccount("T-reset-b-live"))!.ExpireDate.Should().BeAfter(after_expire);
    }
}
